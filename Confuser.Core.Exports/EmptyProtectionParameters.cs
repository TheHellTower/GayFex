using System;
using System.Collections.Immutable;
using dnlib.DotNet;

namespace Confuser.Core {
	/// <inheritdoc cref="IProtectionParameters" />
	public sealed class EmptyProtectionParameters : IProtectionParameters {
		public static IProtectionParameters Instance { get; } = new EmptyProtectionParameters();

		IImmutableList<IDnlibDef> IProtectionParameters.Targets => ImmutableArray.Create<IDnlibDef>();

		private EmptyProtectionParameters() { }

		T IProtectionParameters.GetParameter<T>(IConfuserContext context, IDnlibDef target, IProtectionParameter<T> parameter) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (target == null) throw new ArgumentNullException(nameof(target));
			if (parameter == null) throw new ArgumentNullException(nameof(parameter));

			return parameter.DefaultValue;
		}
	}
}
