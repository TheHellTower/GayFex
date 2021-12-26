using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Helpers;
using Confuser.Protections.Compress;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using dnlib.DotNet.Writer;
using dnlib.PE;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FileAttributes = dnlib.DotNet.FileAttributes;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using SR = System.Reflection;

namespace Confuser.Protections {
	[Export(typeof(IPacker))]
	[ExportMetadata(nameof(IPackerMetadata.Id), _FullId)]
	[ExportMetadata(nameof(IPackerMetadata.MarkerId), _Id)]
	internal sealed class Compressor : IPacker {
		internal const string _Id = "compressor";
		internal const string _FullId = "Ki.Compressor";
		internal static readonly object ContextKey = new object();

		public string Name => "Compressing Packer";

		public string Description => "This packer reduces the size of output.";

		internal CompressorParameters Parameters { get; } = new CompressorParameters();

		void IConfuserComponent.Initialize(IServiceCollection collectin) {
		}

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) =>
			pipeline.InsertPreStage(PipelineStage.WriteModule, new ExtractPhase(this));

		void IPacker.Pack(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			var ctx = context.Annotations.Get<CompressorContext>(context, ContextKey);
			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("compressor");
			if (ctx == null) {
				logger.LogCritical("No executable module!");
				throw new ConfuserException();
			}

			var originModule = context.Modules[ctx.ModuleIndex];
			ctx.OriginModuleDef = originModule;

			var stubModule = new ModuleDefUser(ctx.ModuleName, originModule.Mvid, originModule.CorLibTypes.AssemblyRef);
			if (ctx.CompatMode) {
				var assembly = new AssemblyDefUser(originModule.Assembly);
				assembly.Name += ".cr";
				assembly.Modules.Add(stubModule);
			}
			else {
				ctx.Assembly.Modules.Insert(0, stubModule);
				ImportAssemblyTypeReferences(originModule, stubModule);
			}

			stubModule.Context.AssemblyResolver = originModule.Context.AssemblyResolver;
			stubModule.Context.Resolver = originModule.Context.Resolver;

			stubModule.Characteristics = originModule.Characteristics;
			stubModule.Cor20HeaderFlags = originModule.Cor20HeaderFlags;
			stubModule.Cor20HeaderRuntimeVersion = originModule.Cor20HeaderRuntimeVersion;
			stubModule.DllCharacteristics = originModule.DllCharacteristics;
			stubModule.EncBaseId = originModule.EncBaseId;
			stubModule.EncId = originModule.EncId;
			stubModule.Generation = originModule.Generation;
			stubModule.Kind = ctx.Kind;
			stubModule.Machine = originModule.Machine;
			stubModule.RuntimeVersion = originModule.RuntimeVersion;
			stubModule.TablesHeaderVersion = originModule.TablesHeaderVersion;
			stubModule.Win32Resources = originModule.Win32Resources;

			InjectStub(context, ctx, parameters, stubModule, token);

			var markerService = context.Registry.GetRequiredService<IMarkerService>();

			var snKeyData = markerService.GetStrongNameKey(context, originModule);

			using (var ms = new MemoryStream()) {
				var options = new ModuleWriterOptions(stubModule) {
					StrongNameKey = snKeyData.SnKey,
					StrongNamePublicKey = snKeyData.SnPubKey,
					DelaySign = snKeyData.SnDelaySign
				};
				var injector = new KeyInjector(ctx);
				options.WriterEvent += injector.WriterEvent;

				stubModule.Write(ms, options);
				token.ThrowIfCancellationRequested();

				var packerService = context.Registry.GetRequiredService<IPackerService>();
				packerService.ProtectStub(context, context.OutputPaths[ctx.ModuleIndex], ms.ToArray(), snKeyData,
					new StubProtection(ctx, originModule), token);
			}
		}

		private static string GetId(ReadOnlyMemory<byte> module) {
			var md = MetadataFactory.CreateMetadata(new PEImage(module.ToArray()));
			var assembly = new AssemblyNameInfo();

			// ReSharper disable once InvertIf
			if (md.TablesStream.TryReadAssemblyRow(1, out var assemblyRow)) {
				assembly.Name = md.StringsStream.ReadNoNull(assemblyRow.Name);
				assembly.Culture = md.StringsStream.ReadNoNull(assemblyRow.Locale);
				assembly.PublicKeyOrToken = new PublicKey(md.BlobStream.Read(assemblyRow.PublicKey));
				assembly.HashAlgId = (AssemblyHashAlgorithm)assemblyRow.HashAlgId;
				assembly.Version = new Version(assemblyRow.MajorVersion, assemblyRow.MinorVersion,
					assemblyRow.BuildNumber, assemblyRow.RevisionNumber);
				assembly.Attributes = (AssemblyAttributes)assemblyRow.Flags;
			}

			return GetId(assembly);
		}

		private static string GetId(IFullName assembly) =>
			new SR.AssemblyName(assembly.FullName).FullName.ToUpperInvariant();

		private static void PackModules(IConfuserContext context, CompressorContext compCtx, ModuleDef stubModule,
			ICompressionService comp, IRandomGenerator random, ILogger logger, CancellationToken token) {
			int maxLen = 0;
			var modules = new Dictionary<string, ReadOnlyMemory<byte>>();
			for (int i = 0; i < context.OutputModules.Count; i++) {
				if (i == compCtx.ModuleIndex)
					continue;

				string id = GetId(context.Modules[i].Assembly);
				modules.Add(id, context.OutputModules[i]);

				int strLen = Encoding.UTF8.GetByteCount(id);
				if (strLen > maxLen)
					maxLen = strLen;
			}

			foreach (var extModule in context.ExternalModules) {
				var name = GetId(extModule);
				modules.Add(name, extModule);

				int strLen = Encoding.UTF8.GetByteCount(name);
				if (strLen > maxLen)
					maxLen = strLen;
			}

			var key = random.NextBytes(4 + maxLen);
			var keySpan = key.Span;
			keySpan[0] = (byte)(compCtx.EntryPointToken >> 0);
			keySpan[1] = (byte)(compCtx.EntryPointToken >> 8);
			keySpan[2] = (byte)(compCtx.EntryPointToken >> 16);
			keySpan[3] = (byte)(compCtx.EntryPointToken >> 24);
			for (int i = 4; i < key.Length; i++) // no zero bytes
				keySpan[i] |= 1;
			compCtx.KeySig = key;

			foreach (var entry in modules) {
				var name = Encoding.UTF8.GetBytes(entry.Key);
				for (int i = 0; i < name.Length; i++)
					name[i] *= keySpan[i + 4];

				uint state = name.Aggregate<byte, uint>(0x6fff61, (current, chr) => current * 0x5e3f1f + chr);
				var encrypted = compCtx.Encrypt(comp, entry.Value, state, delegate { });
				token.ThrowIfCancellationRequested();

				var resource = new EmbeddedResource(Convert.ToBase64String(name), encrypted.ToArray());
				stubModule.Resources.Add(resource);
			}

			//logger.EndProgress();
		}

		private PlaceholderProcessor InjectData(IConfuserContext context, ModuleDef stubModule,
			ReadOnlyMemory<byte> data) {
			Debug.Assert(context != null, $"{nameof(context)} != null");
			Debug.Assert(stubModule != null, $"{nameof(stubModule)} != null");

			var name = context.Registry.GetService<INameService>();
			var marker = context.Registry.GetRequiredService<IMarkerService>();

			var dataType = new TypeDefUser("", "DataType", stubModule.CorLibTypes.GetTypeRef("System", "ValueType")) {
				Layout = TypeAttributes.ExplicitLayout,
				Visibility = TypeAttributes.NestedPrivate,
				IsSealed = true,
				ClassLayout = new ClassLayoutUser(1, (uint)data.Length)
			};
			stubModule.GlobalType.NestedTypes.Add(dataType);
			stubModule.UpdateRowId(dataType.ClassLayout);
			stubModule.UpdateRowId(dataType);
			name?.MarkHelper(context, dataType, marker, this);
			marker.Mark(context, dataType, this);

			var dataField = new FieldDefUser("DataField", new FieldSig(dataType.ToTypeSig())) {
				IsStatic = true,
				HasFieldRVA = true,
				InitialValue = data.ToArray(),
				Access = FieldAttributes.CompilerControlled
			};
			stubModule.GlobalType.Fields.Add(dataField);
			stubModule.UpdateRowId(dataField);
			// Do not use the naming service. It renames the field and the StubProtection relies on the name of the
			// data field as for right now to find the required field.
			marker.Mark(context, dataField, this);

			return (module, method, args) => {
				var repl = new List<Instruction>(args.Count + 3);
				repl.AddRange(args);
				repl.Add(Instruction.Create(OpCodes.Dup));
				repl.Add(Instruction.Create(OpCodes.Ldtoken, dataField));

				var runtimeHelper =
					stubModule.CorLibTypes.GetTypeRef("System.Runtime.CompilerServices", "RuntimeHelpers");
				var initArrayDef = runtimeHelper.ResolveThrow().FindMethod("InitializeArray");
				repl.Add(Instruction.Create(OpCodes.Call, stubModule.Import(initArrayDef)));
				return repl;
			};
		}

		private void InjectStub(IConfuserContext context, CompressorContext compCtx, IProtectionParameters parameters,
			ModuleDef stubModule, CancellationToken token) {
			var rt = context.Registry.GetRequiredService<IRuntimeService>();
			var random = context.Registry.GetRequiredService<IRandomService>().GetRandomGenerator(_FullId);
			var comp = context.Registry.GetRequiredService<ICompressionService>();
			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("compressor");

			//logger.Debug("Encrypting modules...");

			switch (parameters.GetParameter(context, context.CurrentModule, Parameters.Key)) {
				case KeyDeriverMode.Normal:
					compCtx.Deriver = new NormalDeriver();
					break;
				case KeyDeriverMode.Dynamic:
					compCtx.Deriver = new DynamicDeriver();
					break;
				default:
					throw new UnreachableException();
			}

			compCtx.Deriver.Init(context, random);

			var rtType = GetRuntimeType(stubModule, context, compCtx, logger);
			var mainMethod = rtType?.FindMethod("Main");
			if (mainMethod == null) {
				logger.LogCritical("Runtime type for compressor not available. Packed assembly can't work.");
				throw new ConfuserException();
			}

			uint seed = random.NextUInt32();
			compCtx.OriginModule = context.OutputModules[compCtx.ModuleIndex];

			var encryptedModule = compCtx.Encrypt(comp, compCtx.OriginModule, seed,
				progress => { }); // logger.Progress((int)(progress * 10000), 10000));
			token.ThrowIfCancellationRequested();

			compCtx.EncryptedModule = encryptedModule;

			var mutationKeys = ImmutableDictionary.Create<MutationField, int>()
				.Add(MutationField.KeyI0, encryptedModule.Length >> 2)
				.Add(MutationField.KeyI1, (int)seed);
			compCtx.KeyTokenLoadUpdate = new LateMutationFieldUpdate();
			var lateMutationKeys = ImmutableDictionary.Create<MutationField, LateMutationFieldUpdate>()
				.Add(MutationField.KeyI2, compCtx.KeyTokenLoadUpdate);

			var injectHelper = new InjectHelper(context);

			var injectResult = injectHelper.Inject(mainMethod, stubModule,
				InjectBehaviors.RenameAndNestBehavior(context, stubModule.GlobalType),
				new CompressionServiceProcessor(context, stubModule),
				new MutationProcessor(context.Registry, stubModule) {
					KeyFieldValues = mutationKeys,
					LateKeyFieldValues = lateMutationKeys,
					CryptProcessor = compCtx.Deriver.EmitDerivation(context),
					PlaceholderProcessor = InjectData(context, stubModule, encryptedModule)
				});

			// Main
			var entryPoint = injectResult.Requested.Mapped;
			stubModule.EntryPoint = entryPoint;

			if (compCtx.EntryPoint.HasAttribute("System.STAThreadAttribute")) {
				var attrType = stubModule.CorLibTypes.GetTypeRef("System", "STAThreadAttribute");
				var ctorSig = MethodSig.CreateInstance(stubModule.CorLibTypes.Void);
				entryPoint.CustomAttributes.Add(new CustomAttribute(
					new MemberRefUser(stubModule, ".ctor", ctorSig, attrType)));
			}
			else if (compCtx.EntryPoint.HasAttribute("System.MTAThreadAttribute")) {
				var attrType = stubModule.CorLibTypes.GetTypeRef("System", "MTAThreadAttribute");
				var ctorSig = MethodSig.CreateInstance(stubModule.CorLibTypes.Void);
				entryPoint.CustomAttributes.Add(new CustomAttribute(
					new MemberRefUser(stubModule, ".ctor", ctorSig, attrType)));
			}
			//logger.EndProgress();

			// Pack modules
			PackModules(context, compCtx, stubModule, comp, random, logger, token);
		}

		private static void ImportAssemblyTypeReferences(ModuleDef originModule, ModuleDef stubModule) {
			var assembly = stubModule.Assembly;
			foreach (var ca in assembly.CustomAttributes) {
				if (ca.AttributeType.Scope == originModule)
					ca.Constructor = (ICustomAttributeType)stubModule.Import(ca.Constructor);
			}

			foreach (var ca in assembly.DeclSecurities.SelectMany(declSec => declSec.CustomAttributes)) {
				if (ca.AttributeType.Scope == originModule)
					ca.Constructor = (ICustomAttributeType)stubModule.Import(ca.Constructor);
			}
		}

		private sealed class KeyInjector {
			private readonly CompressorContext _ctx;

			public KeyInjector(CompressorContext ctx) => _ctx = ctx;

			public void WriterEvent(object sender, ModuleWriterEventArgs args) =>
				OnWriterEvent(args.Writer, args.Event);

			private void OnWriterEvent(ModuleWriterBase writer, ModuleWriterEvent evt) {
				// ReSharper disable once SwitchStatementMissingSomeCases
				switch (evt) {
					case ModuleWriterEvent.MDBeginCreateTables: {
						// Add key signature
						uint sigBlob = writer.Metadata.BlobHeap.Add(_ctx.KeySig.ToArray());
						uint sigRid =
							writer.Metadata.TablesHeap.StandAloneSigTable.Add(new RawStandAloneSigRow(sigBlob));
						Debug.Assert(sigRid == 1);
						uint sigToken = 0x11000000 | sigRid;
						_ctx.KeyToken = sigToken;
						_ctx.KeyTokenLoadUpdate.ApplyValue((int)sigToken);
						break;
					}
					case ModuleWriterEvent.MDBeginAddResources when !_ctx.CompatMode: {
						// Compute hash
						var hash = SHA1.Create().ComputeHash(_ctx.OriginModule.ToArray());
						uint hashBlob = writer.Metadata.BlobHeap.Add(hash);

						var fileTbl = writer.Metadata.TablesHeap.FileTable;
						uint fileRid = fileTbl.Add(new RawFileRow(
							(uint)FileAttributes.ContainsMetadata,
							writer.Metadata.StringsHeap.Add("koi"),
							hashBlob));
						uint impl = CodedToken.Implementation.Encode(new MDToken(Table.File, fileRid));

						// Add resources
						var resTbl = writer.Metadata.TablesHeap.ManifestResourceTable;
						foreach (var resource in _ctx.ManifestResources)
							resTbl.Add(new RawManifestResourceRow(resource.Offset, resource.Flags,
								writer.Metadata.StringsHeap.Add(resource.Value), impl));

						// Add exported types
						// This creates a meta data warning in peverify, stating that the exported type has no TypeDefId.
						// Is this even required?

						//  var exTbl = writer.Metadata.TablesHeap.ExportedTypeTable;
						//  foreach (var type in ctx.OriginModuleDef.GetTypes()) {
						//  	if (!type.IsVisibleOutside())
						//  		continue;
						//  	exTbl.Add(new RawExportedTypeRow((uint)type.Attributes, 0,
						//  									 writer.Metadata.StringsHeap.Add(type.Name),
						//  									 writer.Metadata.StringsHeap.Add(type.Namespace), impl));
						//  }
						break;
					}
				}
			}
		}

		private static TypeDef GetRuntimeType(ModuleDef module, IConfuserContext context, CompressorContext compCtx,
			ILogger logger) {
			Debug.Assert(module != null, $"{nameof(module)} != null");
			Debug.Assert(context != null, $"{nameof(context)} != null");
			Debug.Assert(compCtx != null, $"{nameof(compCtx)} != null");
			Debug.Assert(logger != null, $"{nameof(logger)} != null");

			var rt = context.Registry.GetRequiredService<ProtectionsRuntimeService>().GetRuntimeModule();

			string runtimeTypeName =
				(compCtx.CompatMode ? "Confuser.Runtime.CompressorCompat" : "Confuser.Runtime.Compressor");

			TypeDef rtType = null;
			try {
				rtType = rt.GetRuntimeType(runtimeTypeName, module);
			}
			catch (ArgumentException ex) {
				logger.LogError("Failed to load runtime: {0}", ex.Message);
				return null;
			}

			if (rtType == null) {
				logger.LogError("Failed to load runtime: {0}", runtimeTypeName);
				return null;
			}

			return rtType;
		}
	}
}
