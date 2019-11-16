using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Protections.Constants {
	using ReferenceList = List<(MethodDef Method, Instruction Instruction, TypeSig TypeSig)>;
	using BlockReferenceList = List<(MethodDef Method, Instruction Instruction, TypeSig TypeSig, int Size)>;

	internal sealed partial class ExtractPhase : IProtectionPhase {

		public ExtractPhase(ConstantProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public ConstantProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Methods;

		public bool ProcessAll => false;

		public string Name => "Constants extraction";

		public void Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			var moduleCtx = context.Annotations.Get<CEContext>(context.CurrentModule, ConstantProtection.ContextKey);
			if (!parameters.Targets.Any() || moduleCtx == null)
				return;

			var ldc = new Dictionary<object, ReferenceList>();
			var ldInit = new Dictionary<byte[], ReferenceList>(new ByteArrayComparer());
			var dataFields = new Dictionary<FieldDef, List<Instruction>>(FieldEqualityComparer.CompareDeclaringTypes);

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(ConstantProtection._Id);

			// Extract constants
			ExtractConstants(context, parameters, moduleCtx, ldc, ldInit, dataFields, logger, token);
			logger.LogMsgExtractedFromModule(moduleCtx.Module, ldc.Count + ldInit.Count);

			if (!ldc.Any() && !ldInit.Any()) return;

			// Create data block
			var dataBlock = CreateDataBlock(moduleCtx, ldc, ldInit, logger);

			// Compress data block
			var (encodedData, encodedReferences) = CompressDataBlock(context, parameters, moduleCtx, dataBlock, logger);
			moduleCtx.EncodedData = encodedData;
			moduleCtx.EncodedReferences = encodedReferences;
			moduleCtx.EncodedDataFields = dataFields;
		}

		private void ExtractConstants(
			IConfuserContext context,
			IProtectionParameters parameters,
			CEContext moduleCtx,
			IDictionary<object, ReferenceList> ldc,
			IDictionary<byte[], ReferenceList> ldInit,
			IDictionary<FieldDef, List<Instruction>> dataFields,
			ILogger logger, CancellationToken token) {
			foreach (var method in parameters.Targets.OfType<MethodDef>()) {
				if (!method.HasBody)
					continue;

				// Skip all members that were introduced for the constant encoding itself
				// to avoid creating endless loops.
				if (moduleCtx.Marker.GetHelperParent(method) == Parent)
					continue;


				moduleCtx.Elements = parameters.GetParameter(context, method, Parent.Parameters.Elements);
				if (moduleCtx.Elements == EncodeElements.None)
					continue;

				logger.LogMsgExtractingFromMethod(method);

				var ldcOldCount = ldc.Count;
				var ldInitOldCount = ldInit.Count;

				foreach (var instr in method.Body.Instructions) {
					TypeSig signature = null;
					// ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
					switch (instr.OpCode.Code) {
						case Code.Ldstr:
							if (moduleCtx.EncodeStrings) {
								var strOperand = (string)instr.Operand;
								if (!string.IsNullOrEmpty(strOperand) || moduleCtx.EncodePrimitive) 
									signature = moduleCtx.Module.CorLibTypes.String;
							}

							break;
						case Code.Ldc_I4:
							if (moduleCtx.EncodeNumbers) {
								var intOperand = (int)instr.Operand;
								if (intOperand < -1 || intOperand > 8 || moduleCtx.EncodePrimitive) 
									signature = moduleCtx.Module.CorLibTypes.Int32;
							}

							break;
						case Code.Ldc_I8:
							if (moduleCtx.EncodeNumbers) 
								signature = moduleCtx.Module.CorLibTypes.Int64;

							break;
						case Code.Ldc_R4:
							if (moduleCtx.EncodeNumbers) 
								signature = moduleCtx.Module.CorLibTypes.Single;

							break;
						case Code.Ldc_R8:
							if (moduleCtx.EncodeNumbers) 
								signature = moduleCtx.Module.CorLibTypes.Double;

							break;
					}

					if (signature != null) {
						ldc.AddListEntry(instr.Operand, (method, instr, signature));
						logger.LogMsgFoundConstantValue(method, instr.Operand);
						continue;
					}

					if (instr.OpCode.Code != Code.Call ||
						!moduleCtx.EncodeInitializers ||
						!(instr.Operand is IMethod operand) ||
						!IsArrayInitializer(operand)) continue;

					var instrs = method.Body.Instructions;
					int i = instrs.IndexOf(instr);
					if (i < 5) continue;
					if (instrs[i - 1].OpCode != OpCodes.Ldtoken) continue;
					if (instrs[i - 2].OpCode != OpCodes.Dup) continue;
					if (instrs[i - 3].OpCode != OpCodes.Newarr) continue;
					if (instrs[i - 4].OpCode != OpCodes.Ldc_I4) continue;

					if (!(instrs[i - 1].Operand is FieldDef dataField))
						continue;
					if (!dataField.HasFieldRVA || dataField.InitialValue == null)
						continue;

					// Prevent array length from being encoded
					var arrLen = (int)instrs[i - 4].Operand;
					if (ldc.ContainsKey(arrLen)) {
						var list = ldc[arrLen];
						list.RemoveWhere(entry => entry.Item2 == instrs[i - 4]);
						if (list.Count == 0)
							ldc.Remove(arrLen);
					}

					dataFields.AddListEntry(dataField, instrs[i - 1]);

					var value = new byte[dataField.InitialValue.Length + 4];
					value[0] = (byte)(arrLen >> 0);
					value[1] = (byte)(arrLen >> 8);
					value[2] = (byte)(arrLen >> 16);
					value[3] = (byte)(arrLen >> 24);
					Buffer.BlockCopy(dataField.InitialValue, 0, value, 4, dataField.InitialValue.Length);

					var arrType = new SZArraySig(((ITypeDefOrRef)instrs[i - 3].Operand).ToTypeSig());
					ldInit.AddListEntry(value, (method, instr, arrType));
					logger.LogMsgFoundConstantInitializer(method, value);
				}

				logger.LogMsgExtractedFromMethod(method, ldc.Count + ldInit.Count - ldcOldCount - ldInitOldCount);
				token.ThrowIfCancellationRequested();
			}
		}

		private (List<int> Data, Dictionary<int, BlockReferenceList> References) CreateDataBlock(CEContext moduleCtx, IDictionary<object, ReferenceList> ldc, IDictionary<byte[], ReferenceList> ldInit, ILogger logger) {
			var dataBlocks = new List<(List<int> Data, Dictionary<int, BlockReferenceList> References)>();

			var maxSize = ldc.Keys.Select(GetSizeOf).Concat(ldInit.Keys.Select(a => a.Length)).Sum(s => RoundUp(s, sizeof(int))).Bytes();
			logger.LogMsgCreatingDataBlockStart(moduleCtx.Module, maxSize);

			var sortedLdc = ldc.Select(GetDataAndRefs);
			var sortedLdInit = ldInit.Select(GetDataAndRefs);

			// 1st run
			//   Check all the data blocks and merge those that are completely overlapping.
			//   This likely get's rid of all the numeric values and just leaves the data for strings and arrays as distinct data blocks.
			foreach (var (data, references) in sortedLdInit.Concat(sortedLdc).OrderByDescending(e => e.Data.Length)) {
				var matchingDone = false;
				foreach (var (blockData, blockReferences) in dataBlocks) {
					var matchingIndex = IndexOf(blockData, data);
					if (matchingIndex < 0) continue;
					blockReferences.AddListEntries(matchingIndex, references.Select(r => (r.Method, r.Instruction, r.TypeSig, data.Length)));
					matchingDone = true;
					break;
				}

				if (matchingDone) continue;

				// no overlap with any existing data block -> Create a new one
				dataBlocks.Add((
					new List<int>(data.ToArray()),
					new Dictionary<int, BlockReferenceList> { { 0, references.Select(r => (r.Method, r.Instruction, r.TypeSig, data.Length)).ToList() } }));
			}

			logger.LogMsgCreatingDataBlockFirstPassDone(moduleCtx.Module, dataBlocks.Count);

			var lastBlockCount = -1;
			while (dataBlocks.Count > 1 && lastBlockCount != dataBlocks.Count) {
				// In every pass we are looking for two data blocks to combine.
				// The data blocks selected, are the one with the largest overlap.

				lastBlockCount = dataBlocks.Count;
				var fullOverlapIndex = -1;
				var partialOverlapOffset = 0;
				var overlapIndex = (-1, -1);

				for (var i = 0; i < dataBlocks.Count - 1; i++) {
					var block1 = dataBlocks[i];
					for (var k = i + 1; k < dataBlocks.Count; k++) {
						var block2 = dataBlocks[k];

						fullOverlapIndex = IndexOf(block1.Data, block2.Data);
						if (fullOverlapIndex != -1) {
							overlapIndex = (i, k);
							break;
						}

						fullOverlapIndex = IndexOf(block2.Data, block1.Data);
						if (fullOverlapIndex != -1) {
							overlapIndex = (k, i);
							break;
						}

						var thisPartialOverlapOffset = GetOverlap(block1.Data, block2.Data);
						if (GetOverlappingSize(block1.Data.Count, thisPartialOverlapOffset) >
							GetOverlappingSize(block1.Data.Count, partialOverlapOffset)) {
							partialOverlapOffset = thisPartialOverlapOffset;
							overlapIndex = (i, k);
						}

					}

					if (fullOverlapIndex != -1) break;
				}

				if (fullOverlapIndex != -1) {
					var block1 = dataBlocks[overlapIndex.Item1];
					var block2 = dataBlocks[overlapIndex.Item2];

					foreach (var reference in block2.References)
						block1.References.AddListEntries(reference.Key + fullOverlapIndex, reference.Value);

					dataBlocks.RemoveAt(overlapIndex.Item2);
				}
				else if (partialOverlapOffset != 0) {
					var block1 = dataBlocks[overlapIndex.Item1];
					var block2 = dataBlocks[overlapIndex.Item2];
					var realOffset = partialOverlapOffset;

					if (realOffset < 0) {
						var tmp = block1;
						block1 = block2;
						block2 = tmp;

						realOffset *= -1;
					}

					block1.Data.AddRange(block2.Data.Skip(realOffset));

					foreach (var reference in block2.References)
						block1.References.AddListEntries(reference.Key + realOffset, reference.Value);

					dataBlocks.RemoveAt(partialOverlapOffset > 0 ? overlapIndex.Item2 : overlapIndex.Item1);
				}
			}

			logger.LogMsgCreatingDataBlockSecondPassDone(moduleCtx.Module, dataBlocks.Count);

			for (var i = dataBlocks.Count - 1; i > 0; i--) {
				var block1 = dataBlocks[0];
				var block2 = dataBlocks[i];

				var block1Length = block1.Data.Count;

				block1.Data.AddRange(block2.Data);
				foreach (var reference in block2.References)
					block1.References.AddListEntries(reference.Key + block1Length, reference.Value);

				dataBlocks.RemoveAt(i);
			}

			Debug.Assert(dataBlocks.Count == 1);
			logger.LogMsgCreatingDataBlockDone(moduleCtx.Module, (dataBlocks[0].Data.Count * sizeof(int)).Bytes(), maxSize);

			return dataBlocks[0];
		}

		private (Memory<byte> Data, Dictionary<int, BlockReferenceList> References) CompressDataBlock(IConfuserContext context, 
			IProtectionParameters parameters, 
			CEContext moduleCtx,
			(List<int> Data, Dictionary<int, BlockReferenceList> References) dataBlock, ILogger logger) {

			logger.LogMsgCompressDataBlockStart(moduleCtx.Module, (dataBlock.Data.Count * sizeof(int)).Bytes());
			
			var compressionAlgorithm = parameters.GetParameter(context, moduleCtx.Module, Parent.Parameters.Compressor);
			var compressionMode = parameters.GetParameter(context, moduleCtx.Module, Parent.Parameters.Compress);

			logger.LogMsgCompressDataBlockParameters(moduleCtx.Module, compressionAlgorithm, compressionMode);

			// compress
			var encodedBuff = new byte[dataBlock.Data.Count * 4];
			int buffIndex = 0;
			foreach (var dat in dataBlock.Data) {
				encodedBuff[buffIndex++] = (byte)((dat >> 0) & 0xff);
				encodedBuff[buffIndex++] = (byte)((dat >> 8) & 0xff);
				encodedBuff[buffIndex++] = (byte)((dat >> 16) & 0xff);
				encodedBuff[buffIndex++] = (byte)((dat >> 24) & 0xff);
			}

			Debug.Assert(buffIndex == encodedBuff.Length);
			if (compressionMode != CompressionMode.Off && compressionAlgorithm != CompressionAlgorithm.None) {
				var oldBuffer = encodedBuff;
				encodedBuff = context.Registry.GetService<ICompressionService>().Compress(compressionAlgorithm, encodedBuff);
				logger.LogMsgCompressDataBlockResult(moduleCtx.Module, encodedBuff.Length.Bytes());
				if (compressionMode == CompressionMode.Auto && encodedBuff.Length >= oldBuffer.Length) {
					encodedBuff = oldBuffer;
					compressionAlgorithm = CompressionAlgorithm.None;
					logger.LogMsgCompressDataBlockIsTooLarge(moduleCtx.Module);
				}
			}
			else
				compressionAlgorithm = CompressionAlgorithm.None;

			moduleCtx.UsedCompressionAlgorithm = compressionAlgorithm;

			return (encodedBuff, dataBlock.References);
		}


		private static bool IsArrayInitializer(IMemberRef method) =>
			method.DeclaringType.DefinitionAssembly.IsCorLib() &&
			method.DeclaringType.Namespace == "System.Runtime.CompilerServices" &&
			method.DeclaringType.Name == "RuntimeHelpers" &&
			method.Name == "InitializeArray";

		private static int GetSizeOf(object value) {
			switch (value) {
				case int _:
					return sizeof(int);
				case long _:
					return sizeof(long);
				case float _:
					return sizeof(float);
				case double _:
					return sizeof(double);
				case string strValue:
					return Encoding.UTF8.GetByteCount(strValue);
				default:
					return 0;
			}
		}

		private static int RoundUp(int num, int multiple) =>
			(num + multiple - 1) / multiple * multiple;

		private static Memory<int> GetData(Span<byte> src) {
			var values = new int[RoundUp(src.Length, sizeof(int)) / sizeof(int) + 1];
			values[0] = src.Length;
			EncodeByteArray(src, values.AsSpan(1, values.Length - 1));
			return values;
		}

		private static (Memory<int> Data, ReferenceList References) GetDataAndRefs(KeyValuePair<byte[], ReferenceList> kvp) {
			var values = GetData(kvp.Key);
			return (values, kvp.Value);
		}

		private static (Memory<int> Data, ReferenceList References) GetDataAndRefs(KeyValuePair<object, ReferenceList> kvp) {
			int[] data;
			switch (kvp.Key) {
				case int value:
					data = new[] { value };
					break;
				case long value: {
					var transform = new RTransform() { I8 = value };
					data = new[] { transform.Lo, transform.Hi };
				}
				break;
				case float value: {
					var transform = new RTransform() { R4 = value };
					data = new[] { transform.Lo };
				}
				break;
				case double value: {
					var transform = new RTransform() { R8 = value };
					data = new[] { transform.Lo, transform.Hi };
				}
				break;
				case string strValue:
					data = GetData(Encoding.UTF8.GetBytes(strValue)).ToArray();
					break;
				default:
					throw new InvalidOperationException("Unexpected type for constant value: " + kvp.Key.GetType());
			}

			return (data, kvp.Value);
		}

		private static void EncodeByteArray(ReadOnlySpan<byte> src, Span<int> dest) {
			Debug.Assert(RoundUp(src.Length, sizeof(int)) / sizeof(int) <= dest.Length);

			// byte[] -> uint[]
			int integral = src.Length / 4, remainder = src.Length % 4;
			for (int i = 0; i < integral; i++) {
				var data = src[i * 4] | (src[i * 4 + 1] << 8) | (src[i * 4 + 2] << 16) |
						   (src[i * 4 + 3] << 24);
				dest[i] = data;
			}

			if (remainder <= 0) return;

			int baseIndex = integral * 4;
			int r = 0;
			for (int i = 0; i < remainder; i++)
				r |= src[baseIndex + i] << (i * 8);
			dest[integral] = r;
		}

		private static int IndexOf(List<int> storage, Memory<int> data) =>
			IndexOf(storage, data, l => l.Length, (l, i) => l.Span[i]);

		private static int IndexOf(List<int> storage, IList<int> data) =>
			IndexOf(storage, data, l => l.Count, (l, i) => l[i]);

		private static int IndexOf<T>(List<int> storage, T data, Func<T, int> getCount, Func<T, int, int> getData) {
			var nextIndex = 0;
			var lastValidIndex = storage.Count - getCount(data);
			while (nextIndex <= lastValidIndex) {
				var index = storage.IndexOf(getData(data, 0), nextIndex, lastValidIndex - nextIndex + 1);
				if (index < 0) return -1;
				nextIndex = index + 1;
				var sequenceMatches = true;
				for (var i = 0; i < getCount(data); i++) {
					if (storage[i + index] == getData(data, i)) continue;
					sequenceMatches = false;
					break;
				}

				if (sequenceMatches) return index;
			}

			return -1;
		}

		private static int GetOverlap(List<int> first, List<int> second) {
			var fwdOverlap = GetOverlapInternal(first, second);
			var rwdOverlap = GetOverlapInternal(second, first);

			if (fwdOverlap == -1 && rwdOverlap == -1)
				return 0;

			return fwdOverlap >= rwdOverlap ? fwdOverlap : -(second.Count - rwdOverlap);
		}

		private static int GetOverlapInternal(List<int> first, IList<int> second) {
			var nextIndex = Math.Max(first.Count - second.Count, 0);
			while (nextIndex != -1) {
				var index = first.IndexOf(second[0], nextIndex);
				if (index == -1) return -1;
				var sequenceMatches = true;
				for (var i = 0; i < first.Count - index; i++) {
					if (first[i + index] == second[i]) continue;
					sequenceMatches = false;
					break;
				}

				if (sequenceMatches)
					return index;

				nextIndex = index + 1;
			}

			return -1;
		}

		private static int GetOverlappingSize(int blockSize, int overlapIndex) {
			if (overlapIndex > 0)
				return blockSize - overlapIndex;
			else
				return -overlapIndex;
		}
	}
}
