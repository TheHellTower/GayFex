using Confuser.Core;

namespace Confuser.Protections.Constants {
	internal static partial class ReferenceReplacer {
		internal static bool ReplaceReference(ConstantProtection protection, CEContext ctx,
			IProtectionParameters parameters) {
			foreach (var entry in ctx.ReferenceRepl) {
				if (parameters.GetParameter(ctx.Context, entry.Key,
					protection.Parameters.ControlFlowGraphReplacement)) {
					if (!ReplaceCFG(entry.Key, entry.Value, ctx)) return false;
				}
				else {
					if (!ReplaceNormal(entry.Key, entry.Value)) return false;
				}
			}

			return true;
		}
	}
}
