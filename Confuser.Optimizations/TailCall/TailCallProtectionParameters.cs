using Confuser.Core;

namespace Confuser.Optimizations.TailCall {
	internal sealed class TailCallProtectionParameters {
		/// <summary>
		/// This option allows disabling the tail recursion optimization. Once disabled only the tail call optimization
		/// remains active.
		/// </summary>
		internal IProtectionParameter<bool> TailRecursion { get; } = ProtectionParameter.Boolean("tailRecursion", true);
	}
}
