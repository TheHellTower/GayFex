using System.Collections.Immutable;
using dnlib.DotNet;

namespace Confuser.Core {
	public struct EmptyProtectionParameters : IProtectionParameters {
		public IImmutableList<IDnlibDef> Targets => ImmutableArray.Create<IDnlibDef>();

		public T GetParameter<T>(IConfuserContext context, IDnlibDef target, string name, T defValue = default) => defValue;
	}
}
