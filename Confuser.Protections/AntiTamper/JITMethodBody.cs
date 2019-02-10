using System;
using System.Diagnostics;
using System.IO;
using dnlib.DotNet.Writer;
using dnlib.IO;
using dnlib.PE;

namespace Confuser.Protections.AntiTamper {
	internal class JITMethodBody : IChunk {
		private ReadOnlyMemory<byte> _body;
		public JITExceptionHandlerClause[] ExceptionHandlers { get; set; }

		// ReSharper disable once MemberCanBePrivate.Global
		public ReadOnlyMemory<byte> ILCode { get; set; }

		// ReSharper disable once MemberCanBePrivate.Global
		public ReadOnlyMemory<byte> LocalVars { get; set; }

		// ReSharper disable once MemberCanBePrivate.Global
		public uint MaxStack { get; set; }

		// ReSharper disable once MemberCanBePrivate.Global
		public uint MulSeed { get; set; }

		public uint Offset { get; set; }
		public uint Options { get; set; }

		public FileOffset FileOffset { get; private set; }

		public RVA RVA { get; private set; }

		public void SetOffset(FileOffset offset, RVA rva) {
			FileOffset = offset;
			RVA = rva;
		}

		public uint GetFileLength() => (uint)_body.Length + 4;

		public uint GetVirtualSize() => GetFileLength();

		public void WriteTo(DataWriter writer) {
			writer.WriteUInt32((uint)(_body.Length >> 2));
			writer.WriteBytes(_body.ToArray());
		}

		public void Serialize(uint token, uint key, ReadOnlySpan<byte> fieldLayout) {
			Memory<byte> body;
			using (var ms = new MemoryStream()) {
				var writer = new DataWriter(ms);
				foreach (byte i in fieldLayout)
					switch (i) {
						case 0:
							writer.WriteUInt32((uint)ILCode.Length);
							break;
						case 1:
							writer.WriteUInt32(MaxStack);
							break;
						case 2:
							writer.WriteUInt32((uint)ExceptionHandlers.Length);
							break;
						case 3:
							writer.WriteUInt32((uint)LocalVars.Length);
							break;
						case 4:
							writer.WriteUInt32(Options);
							break;
						case 5:
							writer.WriteUInt32(MulSeed);
							break;
						default:
							throw new NotImplementedException("Invalid field layout index.");
					}

				writer.WriteBytes(ILCode.ToArray());
				writer.WriteBytes(LocalVars.ToArray());
				foreach (var clause in ExceptionHandlers) {
					writer.WriteUInt32(clause.Flags);
					writer.WriteUInt32(clause.TryOffset);
					writer.WriteUInt32(clause.TryLength);
					writer.WriteUInt32(clause.HandlerOffset);
					writer.WriteUInt32(clause.HandlerLength);
					writer.WriteUInt32(clause.ClassTokenOrFilterOffset);
				}

				writer.WriteZeroes(4 - ((int)ms.Length & 3)); // pad to 4 bytes
				body = ms.ToArray();
			}

			Debug.Assert(body.Length % 4 == 0);
			// encrypt body
			{
				uint state = token * key;
				uint counter = state;
				var bodySpan = body.Span;
				for (int i = 0; i < body.Length; i += 4) {
					uint data = bodySpan[i] | (uint)(bodySpan[i + 1] << 8) | (uint)(bodySpan[i + 2] << 16) |
					            (uint)(bodySpan[i + 3] << 24);
					bodySpan[i + 0] ^= (byte)(state >> 0);
					bodySpan[i + 1] ^= (byte)(state >> 8);
					bodySpan[i + 2] ^= (byte)(state >> 16);
					bodySpan[i + 3] ^= (byte)(state >> 24);
					state += data ^ counter;
					counter ^= (state >> 5) | (state << 27);
				}
			}

			_body = body;
		}
	}
}
