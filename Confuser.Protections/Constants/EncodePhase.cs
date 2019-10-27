using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Protections.Constants {
	using ReferenceList = List<(MethodDef, Instruction)>;
	using BlockReferenceList = List<(MethodDef Method, Instruction Instruction, TypeSig TypeSig, int Size)>;
	using ReferenceEnumerable = IEnumerable<(MethodDef, Instruction)>;

	internal class EncodePhase : IProtectionPhase {
		public EncodePhase(ConstantProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public ConstantProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Methods;

		public bool ProcessAll => false;

		public string Name => "Constants encoding";

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters,
			CancellationToken token) {
			var moduleCtx = context.Annotations.Get<CEContext>(context.CurrentModule, ConstantProtection.ContextKey);
			if (!parameters.Targets.Any() || moduleCtx == null)
				return;

			var ldc = new Dictionary<object, ReferenceList>();
			var ldInit = new Dictionary<byte[], ReferenceList>();

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("constants");
			
			var cmp = new SigComparer(0, moduleCtx.Module);
			foreach (var kvp in moduleCtx.EncodedReferences) {
				var bufferIndex = kvp.Key;
				foreach (var byType in kvp.Value.GroupBy(t => t.TypeSig)) {
					Func<DecoderDesc, byte> typeIdFunc;

					if (cmp.Equals(byType.Key, moduleCtx.Module.CorLibTypes.Int32) ||
					    cmp.Equals(byType.Key, moduleCtx.Module.CorLibTypes.Int64) ||
					    cmp.Equals(byType.Key, moduleCtx.Module.CorLibTypes.Single) ||
					    cmp.Equals(byType.Key, moduleCtx.Module.CorLibTypes.Double))
						typeIdFunc = desc => desc.NumberID;
					else if (cmp.Equals(byType.Key, moduleCtx.Module.CorLibTypes.String))
						typeIdFunc = desc => desc.StringID;
					else if (byType.Key is SZArraySig)
						typeIdFunc = desc => desc.InitializerID;
					else
						throw new InvalidOperationException("Unexpected type for constant: " + byType.Key.ToString());

					UpdateReference(moduleCtx, byType.Key, byType.Select(t => (t.Method, t.Instruction)), bufferIndex, typeIdFunc);
				}
			}

			var encodedDataFields = new HashSet<FieldDef>(moduleCtx.EncodedDataFields.Keys, FieldEqualityComparer.CompareDeclaringTypes);
			var encodedFieldInstructions = new HashSet<Instruction>(moduleCtx.EncodedDataFields.Values.SelectMany(i => i));
			RemoveDataFieldRefs(context, encodedDataFields, encodedFieldInstructions);

			if (!ReferenceReplacer.ReplaceReference(Parent, moduleCtx, parameters)) return;

			var encodedBuff = moduleCtx.EncodedData;
			uint compressedLen = (uint)(encodedBuff.Length + 3) / 4;
			compressedLen = (compressedLen + 0xfu) & ~0xfu;
			var compressedBuff = new uint[compressedLen];
			Buffer.BlockCopy(encodedBuff.ToArray(), 0, compressedBuff, 0, encodedBuff.Length);
			Debug.Assert(compressedLen % 0x10 == 0);

			// encrypt
			uint keySeed = moduleCtx.Random.NextUInt32();
			var key = new uint[0x10];
			uint state = keySeed;
			for (int i = 0; i < 0x10; i++) {
				state ^= state >> 12;
				state ^= state << 25;
				state ^= state >> 27;
				key[i] = state;
			}

			var encryptedBuffer = new byte[compressedBuff.Length * 4];
			var buffIndex = 0;
			while (buffIndex < compressedBuff.Length) {
				uint[] enc = moduleCtx.ModeHandler.Encrypt(compressedBuff, buffIndex, key);
				for (int j = 0; j < 0x10; j++)
					key[j] ^= compressedBuff[buffIndex + j];
				Buffer.BlockCopy(enc, 0, encryptedBuffer, buffIndex * 4, 0x40);
				buffIndex += 0x10;
			}

			Debug.Assert(buffIndex == compressedBuff.Length);

			moduleCtx.DataField.InitialValue = encryptedBuffer;
			moduleCtx.DataField.HasFieldRVA = true;
			moduleCtx.DataType.ClassLayout = new ClassLayoutUser(0, (uint)encryptedBuffer.Length);

			moduleCtx.EncodingBufferSizeUpdate.ApplyValue(encryptedBuffer.Length / 4);
			moduleCtx.KeySeedUpdate.ApplyValue((int)keySeed);
		}

		void EncodeInitializer(CEContext moduleCtx, byte[] init, ReferenceEnumerable references) {
			int buffIndex = -1;

			foreach (var instr in references) {
				IList<Instruction> instrs = instr.Item1.Body.Instructions;
				int i = instrs.IndexOf(instr.Item2);

				if (buffIndex == -1)
					buffIndex = EncodeByteArray(moduleCtx, init);

				var (method, decoderDesc) = moduleCtx.Decoders[moduleCtx.Random.NextInt32(moduleCtx.Decoders.Count)];
				uint id = (uint)buffIndex | (uint)(decoderDesc.InitializerID << 30);
				id = moduleCtx.ModeHandler.Encode(decoderDesc.Data, moduleCtx, id);

				instrs[i - 4].Operand = (int)id;
				instrs[i - 3].OpCode = OpCodes.Call;
				var arrType = new SZArraySig(((ITypeDefOrRef)instrs[i - 3].Operand).ToTypeSig());
				instrs[i - 3].Operand = new MethodSpecUser(method, new GenericInstMethodSig(arrType));
				instrs.RemoveAt(i - 2);
				instrs.RemoveAt(i - 2);
				instrs.RemoveAt(i - 2);
			}
		}

		void UpdateReference(CEContext moduleCtx, TypeSig valueType, ReferenceEnumerable references, int buffIndex,
			Func<DecoderDesc, byte> typeID) {
			foreach (var instr in references) {
				var (method, decoderDesc) = moduleCtx.Decoders[moduleCtx.Random.NextInt32(moduleCtx.Decoders.Count)];
				var id = (uint)(buffIndex | (typeID(decoderDesc) << 30));
				id = moduleCtx.ModeHandler.Encode(decoderDesc.Data, moduleCtx, id);

				Debug.Assert((method.ReturnType as GenericSig)?.Number == 0);
				var targetDecoder = new MethodSpecUser(method, new GenericInstMethodSig(valueType));

				Debug.Assert(instr.Item2.OpCode != OpCodes.Ldc_I4 || valueType == method.Module.CorLibTypes.Int32);
				Debug.Assert(instr.Item2.OpCode != OpCodes.Ldc_I8 || valueType == method.Module.CorLibTypes.Int64);
				Debug.Assert(instr.Item2.OpCode != OpCodes.Ldc_R4 || valueType == method.Module.CorLibTypes.Single);
				Debug.Assert(instr.Item2.OpCode != OpCodes.Ldc_R8 || valueType == method.Module.CorLibTypes.Double);
				Debug.Assert(instr.Item2.OpCode != OpCodes.Ldstr || valueType == method.Module.CorLibTypes.String);

				moduleCtx.ReferenceRepl.AddListEntry(instr.Item1, (instr.Item2, id, targetDecoder));
			}
		}

		void RemoveDataFieldRefs(IConfuserContext context, HashSet<FieldDef> dataFields,
			HashSet<Instruction> fieldRefs) {
			foreach (var type in context.CurrentModule.GetTypes())
				foreach (var method in type.Methods.Where(m => m.HasBody)) {
					foreach (var instr in method.Body.Instructions)
						if (instr.Operand is FieldDef && !fieldRefs.Contains(instr))
							dataFields.Remove((FieldDef)instr.Operand);
				}

			foreach (var fieldToRemove in dataFields) {
				fieldToRemove.DeclaringType.Fields.Remove(fieldToRemove);
			}
		}
	}
}
