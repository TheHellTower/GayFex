using System;
using dnlib.DotNet;

namespace Confuser.Protections.ReferenceProxy {
	internal sealed partial class StrongMode {
		private sealed class InitMethodDesc {
			public IRPEncoding Encoding { get; }
			public MethodDef Method { get; }
			public int OpCodeIndex { get; }
			public ReadOnlyMemory<int> TokenByteOrder { get; }
			public ReadOnlyMemory<int> TokenNameOrder { get; }

			internal InitMethodDesc(IRPEncoding encoding, MethodDef method, int opCodeIndex, ReadOnlyMemory<int> tokenByteOrder, ReadOnlyMemory<int> tokenNameOrder) {
				Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
				Method = method ?? throw new ArgumentNullException(nameof(method));
				OpCodeIndex = opCodeIndex;
				TokenByteOrder = tokenByteOrder;
				TokenNameOrder = tokenNameOrder;
			}
		}
	}
}
