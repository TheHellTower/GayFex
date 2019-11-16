using System.Collections.Generic;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Constants {
	internal static partial class ReferenceReplacer {
		private struct CfgContext {
			public CEContext Ctx;
			public ControlFlowGraph Graph;
			public BlockKey[] Keys;
			public IRandomGenerator Random;
			public Dictionary<uint, CfgState> StatesMap;
			public Local StateVariable;
		}
	}
}