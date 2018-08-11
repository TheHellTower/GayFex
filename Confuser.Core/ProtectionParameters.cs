using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.Serialization;
using dnlib.DotNet;

namespace Confuser.Core {
	/// <summary>
	///     Parameters of <see cref="ConfuserComponent" />.
	/// </summary>
	public sealed class ProtectionParameters : IProtectionParameters {
		static readonly object ParametersKey = new object();

		/// <summary>
		///     A empty instance of <see cref="ProtectionParameters" />.
		/// </summary>
		public static readonly ProtectionParameters Empty = new ProtectionParameters(null, ImmutableArray.Create<IDnlibDef>());

		readonly IConfuserComponent comp;

		/// <summary>
		///     Initializes a new instance of the <see cref="ProtectionParameters" /> class.
		/// </summary>
		/// <param name="component">The component that this parameters applied to.</param>
		/// <param name="targets">The protection targets.</param>
		internal ProtectionParameters(IConfuserComponent component, IImmutableList<IDnlibDef> targets) {
			comp = component;
			Targets = targets;
		}

		/// <summary>
		///     Gets the targets of protection.
		///     Possible targets are module, types, methods, fields, events, properties.
		/// </summary>
		/// <value>A list of protection targets.</value>
		public IImmutableList<IDnlibDef> Targets { get; private set; }

		/// <summary>
		///     Obtains the value of a parameter of the specified target.
		/// </summary>
		/// <typeparam name="T">The type of the parameter value.</typeparam>
		/// <param name="context">The working context.</param>
		/// <param name="target">The protection target.</param>
		/// <param name="name">The parameter definition to query.</param>
		/// <returns>The value of the parameter.</returns>
		public T GetParameter<T>(IConfuserContext context, IDnlibDef target, IProtectionParameter<T> parameter) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (parameter == null) throw new ArgumentNullException(nameof(parameter));

			if (comp == null) return parameter.DefaultValue;
			if (comp is IPacker && target == null) {
				// Packer parameters are stored in modules
				target = context.Modules[0];
			}
			if (target == null) throw new ArgumentNullException(nameof(target));

			var objParams = context.Annotations.Get<ProtectionSettings>(target, ParametersKey);
			if (objParams == null) return parameter.DefaultValue;
			if (!objParams.TryGetValue(comp, out var parameters)) return parameter.DefaultValue;

			if (parameters.TryGetValue(parameter.Name, out string ret)) {
				try {
					return parameter.Deserialize(ret);
				}
				catch (SerializationException) { }
			}
			return parameter.DefaultValue;
		}

		/// <summary>
		///     Sets the protection parameters of the specified target.
		/// </summary>
		/// <param name="context">The context.</param>
		/// <param name="target">The protection target.</param>
		/// <param name="parameters">The parameters.</param>
		public static void SetParameters(
			IConfuserContext context, IDnlibDef target, ProtectionSettings parameters) {
			context.Annotations.Set(target, ParametersKey, parameters);
		}

		/// <summary>
		///     Gets the protection parameters of the specified target.
		/// </summary>
		/// <param name="context">The context.</param>
		/// <param name="target">The protection target.</param>
		/// <returns>The parameters.</returns>
		public static ProtectionSettings GetParameters(IConfuserContext context, IDnlibDef target) {
			var result = context.Annotations.Get<ProtectionSettings>(target, ParametersKey);
			if (result == null) {
				result = new ProtectionSettings();
				SetParameters(context, target, result);
			}
			return result;
		}
	}
}
