using System.Collections.Immutable;
using dnlib.DotNet;

namespace Confuser.Core {
	/// <summary>
	///     Parameters of <see cref="IConfuserComponent" />.
	/// </summary>
	public interface IProtectionParameters {
		/// <summary>
		///     Gets the targets of protection.
		///     Possible targets are module, types, methods, fields, events, properties.
		/// </summary>
		/// <value>A list of protection targets.</value>
		IImmutableList<IDnlibDef> Targets { get; }

		/// <summary>
		///     Obtains the value of a parameter of the specified target.
		/// </summary>
		/// <typeparam name="T">The type of the parameter value.</typeparam>
		/// <param name="context">The working context.</param>
		/// <param name="target">The protection target.</param>
		/// <param name="name">The parameter definition to query.</param>
		/// <returns>The value of the parameter.</returns>
		T GetParameter<T>(IConfuserContext context, IDnlibDef target, IProtectionParameter<T> parameter);
	}
}
