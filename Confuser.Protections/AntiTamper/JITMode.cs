using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Helpers;
using Confuser.Protections.Services;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using dnlib.DotNet.Writer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Confuser.Protections.AntiTamper {
	internal class JITMode : NormalMode {
		
		private MethodDef _cctor;
		private MethodDef _cctorRepl;
		private ReadOnlyMemory<byte> _fieldLayout;
		private MethodDef _initMethod;
		private uint _key;
		private IRandomGenerator _random;

		protected override void HandleInject(AntiTamperProtection parent, IConfuserContext context, IProtectionParameters parameters) {
			var logger = context.Registry.GetService<ILoggerProvider>().CreateLogger(AntiTamperProtection._Id);
			
			var random = context.Registry.GetService<IRandomService>().GetRandomGenerator(AntiTamperProtection._FullId);
			InitParameters(parent, context, parameters, random);

			var injectResult = InjectRuntime(parent, context, "Confuser.Runtime.AntiTamperJIT");
			if (injectResult == null) {
				logger.LogMsgNormalModeRuntimeMissing();
				return;
			}

			_initMethod = injectResult.Requested.Mapped;
			
			var name = context.Registry.GetService<INameService>();
			var marker = context.Registry.GetRequiredService<IMarkerService>();
			var antiTamper = context.Registry.GetRequiredService<IAntiTamperService>();

			_cctor = context.CurrentModule.GlobalType.FindStaticConstructor();
			_cctorRepl = new MethodDefUser(name.RandomName(),
				MethodSig.CreateStatic(context.CurrentModule.CorLibTypes.Void)) {
				IsStatic = true,
				Access = MethodAttributes.CompilerControlled,
				Body = new CilBody()
			};
			_cctorRepl.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
			context.CurrentModule.GlobalType.Methods.Add(_cctorRepl);
			name.MarkHelper(context, _cctorRepl, marker, parent);

			var methodDataMapping = new Dictionary<FieldDef, int>();
			foreach (var dependency in injectResult) {
				if (dependency.Source is FieldDef depField && depField.DeclaringType.Name == "MethodData") {
					var mapField = (FieldDef)dependency.Mapped;
					switch (depField.Name.String) {
						case "ILCodeSize": methodDataMapping.Add(mapField, 0); break;
						case "MaxStack": methodDataMapping.Add(mapField, 1); break;
						case "EHCount": methodDataMapping.Add(mapField, 2); break;
						case "LocalVars": methodDataMapping.Add(mapField, 3); break;
						case "Options": methodDataMapping.Add(mapField, 4); break;
						case "MulSeed": methodDataMapping.Add(mapField, 5); break;
					}
				}
			}

			foreach (var dependency in injectResult.InjectedDependencies) {
				if (dependency.Mapped is TypeDef mapType && dependency.Source.Name == "MethodData") {
					var fields = mapType.Fields.ToArray();
					random.Shuffle(fields);

					var fieldLayout = new byte[fields.Length];
					for (var i = 0; i < fields.Length; i++) {
						fieldLayout[i] = (byte)methodDataMapping[fields[i]];
					}
					_fieldLayout = fieldLayout;

					mapType.Fields.Clear();
					foreach (var field in fields)
						mapType.Fields.Add(field);

					break;
				}
			}

			antiTamper.ExcludeMethod(context, _cctor);
		}

		protected override IImmutableDictionary<MutationField, int> CreateMutationKeys() => 
			base.CreateMutationKeys().Add(MutationField.KeyI5, (int)_key);

		protected override void InitParameters(AntiTamperProtection parent, IConfuserContext context,
			IProtectionParameters parameters, IRandomGenerator random) {
			base.InitParameters(parent, context, parameters, random);
			_key = random.NextUInt32();
			_random = random;
		}

		protected override void HandleMD(AntiTamperProtection parent, IConfuserContext context, IProtectionParameters parameters) {
			// move initialization away from module initializer
			_cctorRepl.Body = _cctor.Body;
			_cctor.Body = new CilBody();
			_cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, _initMethod));
			_cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, _cctorRepl));
			_cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

			base.HandleMD(parent, context, parameters);
		}
		
		[SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
		protected override void CreateSections(ModuleWriterBase writer) {
			// move some PE parts to separate section to prevent it from being hashed
			var peSection = new PESection("", CNT_CODE | MEM_EXECUTE | MEM_READ);
			bool moved = false;
			uint alignment;
			if (writer.StrongNameSignature != null) {
				alignment = writer.TextSection.Remove(writer.StrongNameSignature).Value;
				peSection.Add(writer.StrongNameSignature, alignment);
				moved = true;
			}

			if (writer is ModuleWriter managedWriter) {
				if (managedWriter.ImportAddressTable != null) {
					alignment = writer.TextSection.Remove(managedWriter.ImportAddressTable).Value;
					peSection.Add(managedWriter.ImportAddressTable, alignment);
					moved = true;
				}
				if (managedWriter.StartupStub != null) {
					alignment = writer.TextSection.Remove(managedWriter.StartupStub).Value;
					peSection.Add(managedWriter.StartupStub, alignment);
					moved = true;
				}
			}
			if (moved)
				writer.Sections.AddBeforeReloc(peSection);

			// create section
			var newSection = new PESection(
				CreateEncryptedSectionName(), 
				CNT_INITIALIZED_DATA | MEM_EXECUTE | MEM_READ | MEM_WRITE);
			writer.Sections.InsertBeforeReloc(_random.NextInt32(writer.Sections.Count), newSection);

			// random padding at beginning to prevent revealing hash key
			newSection.Add(new ByteArrayChunk(_random.NextBytes(0x10).ToArray()), 0x10);

			// create index
			var methodsWithBody = Methods.RemoveAll(m => !m.HasBody);
			var bodyIndex = new JITBodyIndex(methodsWithBody.Select(method => writer.Metadata.GetToken(method).Raw));
			newSection.Add(bodyIndex, 0x10);

			var nopBody = new CilBody {
				Instructions = {
					Instruction.Create(OpCodes.Ldnull),
					Instruction.Create(OpCodes.Throw)
				}
			};

			// save methods
			foreach (var method in methodsWithBody) {//.WithProgress(logger)) {
				var token = writer.Metadata.GetToken(method);

				var jitBody = new JITMethodBody();
				var bodyWriter = new JITMethodBodyWriter(writer.Metadata, method.Body, jitBody, _random.NextUInt32(), writer.Metadata.KeepOldMaxStack || method.Body.KeepOldMaxStack);
				bodyWriter.Write();
				jitBody.Serialize(token.Raw, _key, _fieldLayout.Span);
				bodyIndex.Add(token.Raw, jitBody);

				method.Body = nopBody;
				var methodRow = writer.Metadata.TablesHeap.MethodTable[token.Rid];
				writer.Metadata.TablesHeap.MethodTable[token.Rid] = new RawMethodRow(
					methodRow.RVA,
					(ushort)(methodRow.ImplFlags | (ushort)MethodImplAttributes.NoInlining),
					methodRow.Flags,
					methodRow.Name,
					methodRow.Signature,
					methodRow.ParamList);
			}
			bodyIndex.PopulateSection(newSection);

			// padding to prevent bad size due to shift division
			newSection.Add(new ByteArrayChunk(new byte[4]), 4);
		}
	}
}
