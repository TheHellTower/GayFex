using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using Confuser.Protections.Compress;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using dnlib.DotNet.Writer;
using dnlib.PE;
using Microsoft.Extensions.DependencyInjection;
using FileAttributes = dnlib.DotNet.FileAttributes;
using ILogger = Confuser.Core.ILogger;
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

		void IConfuserComponent.Initialize(IServiceCollection collectin) { }

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) =>
			pipeline.InsertPreStage(PipelineStage.WriteModule, new ExtractPhase(this));

		void IPacker.Pack(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			var ctx = context.Annotations.Get<CompressorContext>(context, ContextKey);
			var logger = context.Registry.GetRequiredService<ILoggingService>().GetLogger("compressor");
			if (ctx == null) {
				logger.Error("No executable module!");
				throw new ConfuserException(null);
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

			var snKey = markerService.GetStrongNameKey(context, originModule);
			using (var ms = new MemoryStream()) {
				var options = new ModuleWriterOptions(stubModule) {
					StrongNameKey = snKey
				};
				var injector = new KeyInjector(ctx);
				options.WriterEvent += injector.WriterEvent;

				stubModule.Write(ms, options);
				token.ThrowIfCancellationRequested();

				var packerService = context.Registry.GetRequiredService<IPackerService>();
				packerService.ProtectStub(context, context.OutputPaths[ctx.ModuleIndex], ms.ToArray(), snKey, new StubProtection(ctx, originModule), token);
			}
		}

		private static string GetId(byte[] module) {
			var md = MetadataFactory.CreateMetadata(new PEImage(module));
			var assembly = new AssemblyNameInfo();
			if (md.TablesStream.TryReadAssemblyRow(1, out var assemblyRow)) {
				assembly.Name = md.StringsStream.ReadNoNull(assemblyRow.Name);
				assembly.Culture = md.StringsStream.ReadNoNull(assemblyRow.Locale);
				assembly.PublicKeyOrToken = new PublicKey(md.BlobStream.Read(assemblyRow.PublicKey));
				assembly.HashAlgId = (AssemblyHashAlgorithm)assemblyRow.HashAlgId;
				assembly.Version = new Version(assemblyRow.MajorVersion, assemblyRow.MinorVersion, assemblyRow.BuildNumber, assemblyRow.RevisionNumber);
				assembly.Attributes = (AssemblyAttributes)assemblyRow.Flags;
			}
			return GetId(assembly);
		}

		private static string GetId(IAssembly assembly) =>
			new SR.AssemblyName(assembly.FullName).FullName.ToUpperInvariant();

		private void PackModules(IConfuserContext context, CompressorContext compCtx, ModuleDef stubModule, ICompressionService comp, IRandomGenerator random, ILogger logger, CancellationToken token) {
			int maxLen = 0;
			var modules = new Dictionary<string, byte[]>();
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
				var name = GetId(extModule).ToUpperInvariant();
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

			int moduleIndex = 0;
			foreach (var entry in modules) {
				byte[] name = Encoding.UTF8.GetBytes(entry.Key);
				for (int i = 0; i < name.Length; i++)
					name[i] *= keySpan[i + 4];

				uint state = 0x6fff61;
				foreach (byte chr in name)
					state = state * 0x5e3f1f + chr;
				var encrypted = compCtx.Encrypt(comp, new ReadOnlyMemory<byte>(entry.Value), state, progress => {
					progress = (progress + moduleIndex) / modules.Count;
					logger.Progress((int)(progress * 10000), 10000);
				});
				token.ThrowIfCancellationRequested();

				var resource = new EmbeddedResource(Convert.ToBase64String(name), encrypted.ToArray(), ManifestResourceAttributes.Private);
				stubModule.Resources.Add(resource);
				moduleIndex++;
			}
			logger.EndProgress();
		}

		private Helpers.PlaceholderProcessor InjectData(IConfuserContext context, ModuleDef stubModule, ReadOnlyMemory<byte> data) {
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

			var dataField = new FieldDefUser("DataField", new FieldSig(dataType.ToTypeSig())) {
				IsStatic = true,
				HasFieldRVA = true,
				InitialValue = data.ToArray(),
				Access = FieldAttributes.CompilerControlled
			};
			stubModule.GlobalType.Fields.Add(dataField);
			stubModule.UpdateRowId(dataField);
			name?.MarkHelper(context, dataField, marker, this);

			return (args) => {
				var repl = new List<Instruction>(args.Count + 3);
				repl.AddRange(args);
				repl.Add(Instruction.Create(OpCodes.Dup));
				repl.Add(Instruction.Create(OpCodes.Ldtoken, dataField));
				repl.Add(Instruction.Create(OpCodes.Call, stubModule.Import(
					typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.InitializeArray)))));
				return repl;
			};
		}

		private void InjectStub(IConfuserContext context, CompressorContext compCtx, IProtectionParameters parameters, ModuleDef stubModule, CancellationToken token) {
			var rt = context.Registry.GetRequiredService<IRuntimeService>();
			var random = context.Registry.GetRequiredService<IRandomService>().GetRandomGenerator(_FullId);
			var comp = context.Registry.GetRequiredService<ICompressionService>();
			var name = context.Registry.GetRequiredService<INameService>();
			var logger = context.Registry.GetRequiredService<ILoggingService>().GetLogger("compressor");
			
			logger.Debug("Encrypting modules...");

			switch (parameters.GetParameter(context, context.CurrentModule, "key", Mode.Normal)) {
				case Mode.Normal:
					compCtx.Deriver = new NormalDeriver();
					break;
				case Mode.Dynamic:
					compCtx.Deriver = new DynamicDeriver();
					break;
				default:
					throw new UnreachableException();
			}
			compCtx.Deriver.Init(context, random);

			var rtType = rt.GetRuntimeType(compCtx.CompatMode ? "Confuser.Runtime.CompressorCompat" : "Confuser.Runtime.Compressor");
			var mainMethod = rtType.FindMethod("Main");
			
			uint seed = random.NextUInt32();
			compCtx.OriginModule = context.OutputModules[compCtx.ModuleIndex];

			var encryptedModule = compCtx.Encrypt(comp, compCtx.OriginModule, seed,
				progress => logger.Progress((int)(progress * 10000), 10000));
			token.ThrowIfCancellationRequested();

			compCtx.EncryptedModule = encryptedModule;

			var mutationKeys = ImmutableDictionary.Create<Helpers.MutationField, int>()
				.Add(Helpers.MutationField.KeyI0, encryptedModule.Length >> 2)
				.Add(Helpers.MutationField.KeyI1, (int)seed);
			compCtx.KeyTokenLoadUpdate = new Helpers.LateMutationFieldUpdate();
			var lateMutationKeys = ImmutableDictionary.Create<Helpers.MutationField, Helpers.LateMutationFieldUpdate>()
				.Add(Helpers.MutationField.KeyI2, compCtx.KeyTokenLoadUpdate);

			var injectResult = Helpers.InjectHelper.Inject(mainMethod, stubModule,
				Helpers.InjectBehaviors.RenameAndNestBehavior(context, stubModule.GlobalType, name),
				new Helpers.MutationProcessor(context.Registry, stubModule) {
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
			logger.EndProgress();

			// Pack modules
			PackModules(context, compCtx, stubModule, comp, random, logger, token);
		}

		void ImportAssemblyTypeReferences(ModuleDef originModule, ModuleDef stubModule) {
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

		private sealed class KeyInjector : IModuleWriterListener {
			readonly CompressorContext ctx;

			public KeyInjector(CompressorContext ctx) =>
				this.ctx = ctx;

			public void WriterEvent(object sender, ModuleWriterEventArgs args) =>
				OnWriterEvent(args.Writer, args.Event);

			public void OnWriterEvent(ModuleWriterBase writer, ModuleWriterEvent evt) {
				if (evt == ModuleWriterEvent.MDBeginCreateTables) {
					// Add key signature
					uint sigBlob = writer.Metadata.BlobHeap.Add(ctx.KeySig.ToArray());
					uint sigRid = writer.Metadata.TablesHeap.StandAloneSigTable.Add(new RawStandAloneSigRow(sigBlob));
					Debug.Assert(sigRid == 1);
					uint sigToken = 0x11000000 | sigRid;
					ctx.KeyToken = sigToken;
					ctx.KeyTokenLoadUpdate.ApplyValue((int)sigToken);
				}
				else if (evt == ModuleWriterEvent.MDBeginAddResources && !ctx.CompatMode) {
					// Compute hash
					byte[] hash = SHA1.Create().ComputeHash(ctx.OriginModule);
					uint hashBlob = writer.Metadata.BlobHeap.Add(hash);

					var fileTbl = writer.Metadata.TablesHeap.FileTable;
					uint fileRid = fileTbl.Add(new RawFileRow(
												   (uint)FileAttributes.ContainsMetadata,
												   writer.Metadata.StringsHeap.Add("koi"),
												   hashBlob));
					uint impl = CodedToken.Implementation.Encode(new MDToken(Table.File, fileRid));

					// Add resources
					var resTbl = writer.Metadata.TablesHeap.ManifestResourceTable;
					foreach (var resource in ctx.ManifestResources)
						resTbl.Add(new RawManifestResourceRow(resource.Offset, resource.Flags, writer.Metadata.StringsHeap.Add(resource.Value), impl));

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
				}
			}
		}
	}
}
