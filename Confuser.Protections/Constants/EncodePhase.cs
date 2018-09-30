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
using ILogger = Confuser.Core.ILogger;

namespace Confuser.Protections.Constants {
	internal class EncodePhase : IProtectionPhase {
		public EncodePhase(ConstantProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public ConstantProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Methods;

		public bool ProcessAll => false;

		public string Name => "Constants encoding";

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			var moduleCtx = context.Annotations.Get<CEContext>(context.CurrentModule, ConstantProtection.ContextKey);
			if (!parameters.Targets.Any() || moduleCtx == null)
				return;

			var ldc = new Dictionary<object, List<Tuple<MethodDef, Instruction>>>();
			var ldInit = new Dictionary<byte[], List<Tuple<MethodDef, Instruction>>>(new ByteArrayComparer());

			var logger = context.Registry.GetRequiredService<ILoggingService>().GetLogger("constants");

			// Extract constants
			ExtractConstants(context, parameters, moduleCtx, ldc, ldInit, logger, token);

			// Encode constants
			moduleCtx.ReferenceRepl = new Dictionary<MethodDef, List<Tuple<Instruction, uint, IMethod>>>();
			moduleCtx.EncodedBuffer = new List<uint>();
			foreach (var entry in ldInit.WithProgress(logger)) // Ensure the array length haven't been encoded yet
			{
				EncodeInitializer(moduleCtx, entry.Key, entry.Value);
				token.ThrowIfCancellationRequested();
			}
			foreach (var entry in ldc.WithProgress(logger)) {
				if (entry.Key is string) {
					EncodeString(moduleCtx, (string)entry.Key, entry.Value);
				}
				else if (entry.Key is int) {
					EncodeConstant32(moduleCtx, (uint)(int)entry.Key, context.CurrentModule.CorLibTypes.Int32, entry.Value);
				}
				else if (entry.Key is long) {
					EncodeConstant64(moduleCtx, (uint)((long)entry.Key >> 32), (uint)(long)entry.Key, context.CurrentModule.CorLibTypes.Int64, entry.Value);
				}
				else if (entry.Key is float) {
					var t = new RTransform();
					t.R4 = (float)entry.Key;
					EncodeConstant32(moduleCtx, t.Lo, context.CurrentModule.CorLibTypes.Single, entry.Value);
				}
				else if (entry.Key is double) {
					var t = new RTransform();
					t.R8 = (double)entry.Key;
					EncodeConstant64(moduleCtx, t.Hi, t.Lo, context.CurrentModule.CorLibTypes.Double, entry.Value);
				}
				else
					throw new UnreachableException();
				token.ThrowIfCancellationRequested();
			}

			if (!ReferenceReplacer.ReplaceReference(Parent, moduleCtx, parameters)) return;

			// compress
			var encodedBuff = new byte[moduleCtx.EncodedBuffer.Count * 4];
			int buffIndex = 0;
			foreach (uint dat in moduleCtx.EncodedBuffer) {
				encodedBuff[buffIndex++] = (byte)((dat >> 0) & 0xff);
				encodedBuff[buffIndex++] = (byte)((dat >> 8) & 0xff);
				encodedBuff[buffIndex++] = (byte)((dat >> 16) & 0xff);
				encodedBuff[buffIndex++] = (byte)((dat >> 24) & 0xff);
			}
			Debug.Assert(buffIndex == encodedBuff.Length);
			encodedBuff = context.Registry.GetService<ICompressionService>().Compress(encodedBuff);
			token.ThrowIfCancellationRequested();

			uint compressedLen = (uint)(encodedBuff.Length + 3) / 4;
			compressedLen = (compressedLen + 0xfu) & ~0xfu;
			var compressedBuff = new uint[compressedLen];
			Buffer.BlockCopy(encodedBuff, 0, compressedBuff, 0, encodedBuff.Length);
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
			buffIndex = 0;
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

		void EncodeString(CEContext moduleCtx, string value, List<Tuple<MethodDef, Instruction>> references) {
			int buffIndex = EncodeByteArray(moduleCtx, Encoding.UTF8.GetBytes(value));

			UpdateReference(moduleCtx, moduleCtx.Module.CorLibTypes.String, references, buffIndex, desc => desc.StringID);
		}

		void EncodeConstant32(CEContext moduleCtx, uint value, TypeSig valueType, List<Tuple<MethodDef, Instruction>> references) {
			int buffIndex = moduleCtx.EncodedBuffer.IndexOf(value);
			if (buffIndex == -1) {
				buffIndex = moduleCtx.EncodedBuffer.Count;
				moduleCtx.EncodedBuffer.Add(value);
			}

			UpdateReference(moduleCtx, valueType, references, buffIndex, desc => desc.NumberID);
		}

		void EncodeConstant64(CEContext moduleCtx, uint hi, uint lo, TypeSig valueType, List<Tuple<MethodDef, Instruction>> references) {
			int buffIndex = -1;
			do {
				buffIndex = moduleCtx.EncodedBuffer.IndexOf(lo, buffIndex + 1);
				if (buffIndex + 1 < moduleCtx.EncodedBuffer.Count && moduleCtx.EncodedBuffer[buffIndex + 1] == hi)
					break;
			} while (buffIndex >= 0);

			if (buffIndex == -1) {
				buffIndex = moduleCtx.EncodedBuffer.Count;
				moduleCtx.EncodedBuffer.Add(lo);
				moduleCtx.EncodedBuffer.Add(hi);
			}

			UpdateReference(moduleCtx, valueType, references, buffIndex, desc => desc.NumberID);
		}

		void EncodeInitializer(CEContext moduleCtx, byte[] init, List<Tuple<MethodDef, Instruction>> references) {
			int buffIndex = -1;

			foreach (var instr in references) {
				IList<Instruction> instrs = instr.Item1.Body.Instructions;
				int i = instrs.IndexOf(instr.Item2);

				if (buffIndex == -1)
					buffIndex = EncodeByteArray(moduleCtx, init);

				Tuple<MethodDef, DecoderDesc> decoder = moduleCtx.Decoders[moduleCtx.Random.NextInt32(moduleCtx.Decoders.Count)];
				uint id = (uint)buffIndex | (uint)(decoder.Item2.InitializerID << 30);
				id = moduleCtx.ModeHandler.Encode(decoder.Item2.Data, moduleCtx, id);

				instrs[i - 4].Operand = (int)id;
				instrs[i - 3].OpCode = OpCodes.Call;
				var arrType = new SZArraySig(((ITypeDefOrRef)instrs[i - 3].Operand).ToTypeSig());
				instrs[i - 3].Operand = new MethodSpecUser(decoder.Item1, new GenericInstMethodSig(arrType));
				instrs.RemoveAt(i - 2);
				instrs.RemoveAt(i - 2);
				instrs.RemoveAt(i - 2);
			}
		}

		int EncodeByteArray(CEContext moduleCtx, byte[] buff) {
			int buffIndex = moduleCtx.EncodedBuffer.Count;
			moduleCtx.EncodedBuffer.Add((uint)buff.Length);

			// byte[] -> uint[]
			int integral = buff.Length / 4, remainder = buff.Length % 4;
			for (int i = 0; i < integral; i++) {
				var data = (uint)(buff[i * 4] | (buff[i * 4 + 1] << 8) | (buff[i * 4 + 2] << 16) | (buff[i * 4 + 3] << 24));
				moduleCtx.EncodedBuffer.Add(data);
			}
			if (remainder > 0) {
				int baseIndex = integral * 4;
				uint r = 0;
				for (int i = 0; i < remainder; i++)
					r |= (uint)(buff[baseIndex + i] << (i * 8));
				moduleCtx.EncodedBuffer.Add(r);
			}
			return buffIndex;
		}

		void UpdateReference(CEContext moduleCtx, TypeSig valueType, List<Tuple<MethodDef, Instruction>> references, int buffIndex, Func<DecoderDesc, byte> typeID) {
			foreach (var instr in references) {
				Tuple<MethodDef, DecoderDesc> decoder = moduleCtx.Decoders[moduleCtx.Random.NextInt32(moduleCtx.Decoders.Count)];
				uint id = (uint)buffIndex | (uint)(typeID(decoder.Item2) << 30);
				id = moduleCtx.ModeHandler.Encode(decoder.Item2.Data, moduleCtx, id);

				var targetDecoder = new MethodSpecUser(decoder.Item1, new GenericInstMethodSig(valueType));
				moduleCtx.ReferenceRepl.AddListEntry(instr.Item1, Tuple.Create(instr.Item2, id, (IMethod)targetDecoder));
			}
		}

		void RemoveDataFieldRefs(IConfuserContext context, HashSet<FieldDef> dataFields, HashSet<Instruction> fieldRefs) {
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

		void ExtractConstants(
			IConfuserContext context, IProtectionParameters parameters, CEContext moduleCtx,
			Dictionary<object, List<Tuple<MethodDef, Instruction>>> ldc,
			Dictionary<byte[], List<Tuple<MethodDef, Instruction>>> ldInit,
			ILogger logger, CancellationToken token) {
			var dataFields = new HashSet<FieldDef>();
			var fieldRefs = new HashSet<Instruction>();
			foreach (MethodDef method in parameters.Targets.OfType<MethodDef>().WithProgress(logger)) {
				if (!method.HasBody)
					continue;

				moduleCtx.Elements = parameters.GetParameter(context, method, Parent.Parameters.Elements);
				if (moduleCtx.Elements == EncodeElements.None)
					continue;

				foreach (Instruction instr in method.Body.Instructions) {
					bool eligible = false;
					if (instr.OpCode == OpCodes.Ldstr && (moduleCtx.Elements & EncodeElements.Strings) != 0) {
						var operand = (string)instr.Operand;
						if (string.IsNullOrEmpty(operand) && (moduleCtx.Elements & EncodeElements.Primitive) == 0)
							continue;
						eligible = true;
					}
					else if (instr.OpCode == OpCodes.Call && (moduleCtx.Elements & EncodeElements.Initializers) != 0) {
						var operand = (IMethod)instr.Operand;
						if (operand.DeclaringType.DefinitionAssembly.IsCorLib() &&
						    operand.DeclaringType.Namespace == "System.Runtime.CompilerServices" &&
						    operand.DeclaringType.Name == "RuntimeHelpers" &&
						    operand.Name == "InitializeArray") {
							IList<Instruction> instrs = method.Body.Instructions;
							int i = instrs.IndexOf(instr);
							if (instrs[i - 1].OpCode != OpCodes.Ldtoken) continue;
							if (instrs[i - 2].OpCode != OpCodes.Dup) continue;
							if (instrs[i - 3].OpCode != OpCodes.Newarr) continue;
							if (instrs[i - 4].OpCode != OpCodes.Ldc_I4) continue;

							var dataField = instrs[i - 1].Operand as FieldDef;
							if (dataField == null)
								continue;
							if (!dataField.HasFieldRVA || dataField.InitialValue == null)
								continue;

							// Prevent array length from being encoded
							var arrLen = (int)instrs[i - 4].Operand;
							if (ldc.ContainsKey(arrLen)) {
								List<Tuple<MethodDef, Instruction>> list = ldc[arrLen];
								list.RemoveWhere(entry => entry.Item2 == instrs[i - 4]);
								if (list.Count == 0)
									ldc.Remove(arrLen);
							}

							dataFields.Add(dataField);
							fieldRefs.Add(instrs[i - 1]);

							var value = new byte[dataField.InitialValue.Length + 4];
							value[0] = (byte)(arrLen >> 0);
							value[1] = (byte)(arrLen >> 8);
							value[2] = (byte)(arrLen >> 16);
							value[3] = (byte)(arrLen >> 24);
							Buffer.BlockCopy(dataField.InitialValue, 0, value, 4, dataField.InitialValue.Length);
							ldInit.AddListEntry(value, Tuple.Create(method, instr));
						}
					}
					else if ((moduleCtx.Elements & EncodeElements.Numbers) != 0) {
						if (instr.OpCode == OpCodes.Ldc_I4) {
							var operand = (int)instr.Operand;
							if ((operand >= -1 && operand <= 8) && (moduleCtx.Elements & EncodeElements.Primitive) == 0)
								continue;
							eligible = true;
						}
						else if (instr.OpCode == OpCodes.Ldc_I8) {
							var operand = (long)instr.Operand;
							if ((operand >= -1 && operand <= 1) && (moduleCtx.Elements & EncodeElements.Primitive) == 0)
								continue;
							eligible = true;
						}
						else if (instr.OpCode == OpCodes.Ldc_R4) {
							var operand = (float)instr.Operand;
							if ((operand == -1 || operand == 0 || operand == 1) && (moduleCtx.Elements & EncodeElements.Primitive) == 0)
								continue;
							eligible = true;
						}
						else if (instr.OpCode == OpCodes.Ldc_R8) {
							var operand = (double)instr.Operand;
							if ((operand == -1 || operand == 0 || operand == 1) && (moduleCtx.Elements & EncodeElements.Primitive) == 0)
								continue;
							eligible = true;
						}
					}

					if (eligible)
						ldc.AddListEntry(instr.Operand, Tuple.Create(method, instr));
				}

				token.ThrowIfCancellationRequested();
			}
			RemoveDataFieldRefs(context, dataFields, fieldRefs);
		}

		class ByteArrayComparer : IEqualityComparer<byte[]> {
			public bool Equals(byte[] x, byte[] y) {
				return x.SequenceEqual(y);
			}

			public int GetHashCode(byte[] obj) {
				int ret = 31;
				foreach (byte v in obj)
					ret = ret * 17 + v;
				return ret;
			}
		}

		[StructLayout(LayoutKind.Explicit)]
		struct RTransform {
			[FieldOffset(0)] public float R4;
			[FieldOffset(0)] public double R8;

			[FieldOffset(4)] public readonly uint Hi;
			[FieldOffset(0)] public readonly uint Lo;
		}
	}
}
