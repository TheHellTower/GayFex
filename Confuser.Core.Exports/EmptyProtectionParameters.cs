using System;
using System.Collections.Immutable;
using dnlib.DotNet;

namespace Confuser.Core {
	public struct EmptyProtectionParameters : IProtectionParameters {
		IImmutableList<IDnlibDef> IProtectionParameters.Targets => ImmutableArray.Create<IDnlibDef>();

		T IProtectionParameters.GetParameter<T>(IConfuserContext context, IDnlibDef target, IProtectionParameter<T> parameter) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (target == null) throw new ArgumentNullException(nameof(target));
			if (parameter == null) throw new ArgumentNullException(nameof(parameter));

			return parameter.DefaultValue;
		}
	}
}
