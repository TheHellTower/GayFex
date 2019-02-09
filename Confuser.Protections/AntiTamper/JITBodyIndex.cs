using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using dnlib.DotNet.Writer;
using dnlib.IO;
using dnlib.PE;

namespace Confuser.Protections.AntiTamper
{
	internal class JITBodyIndex : IChunk {
		private readonly Dictionary<uint, JITMethodBody> _bodies;

		public JITBodyIndex(IEnumerable<uint> tokens) => 
			_bodies = tokens.ToDictionary(token => token, token => (JITMethodBody)null);

		public FileOffset FileOffset { get; private set; }

		public RVA RVA { get; private set; }

		public void SetOffset(FileOffset offset, RVA rva) {
			FileOffset = offset;
			RVA = rva;
		}

		public uint GetFileLength() => (uint)_bodies.Count * 8 + 4;

		public uint GetVirtualSize() => GetFileLength();

		public void WriteTo(DataWriter writer) {
			uint length = GetFileLength() - 4; // minus length field
			writer.WriteUInt32((uint)_bodies.Count);
			foreach (var entry in _bodies.OrderBy(entry => entry.Key)) {
				writer.WriteUInt32(entry.Key);
				Debug.Assert(entry.Value != null);
				Debug.Assert((length + entry.Value.Offset) % 4 == 0);
				writer.WriteUInt32((length + entry.Value.Offset) >> 2);
			}
		}

		public void Add(uint token, JITMethodBody body) {
			Debug.Assert(_bodies.ContainsKey(token));
			_bodies[token] = body;
		}

		public void PopulateSection(PESection section) {
			uint offset = 0;
			foreach (var entry in _bodies.OrderBy(entry => entry.Key)) {
				Debug.Assert(entry.Value != null);
				section.Add(entry.Value, 4);
				entry.Value.Offset = offset;

				Debug.Assert(entry.Value.GetFileLength() % 4 == 0);
				offset += entry.Value.GetFileLength();
			}
		}
	}
}
