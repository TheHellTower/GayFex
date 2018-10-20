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
		/// <exception cref="ArgumentNullException">
		///     <paramref name="context"/> is <see langword="null" />
		///     <br/>- or -<br/>
		///     <paramref name="target"/> is <see langword="null" />
		/// </exception>
		public static void SetParameters(IConfuserContext context, IDnlibDef target, ProtectionSettings parameters) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (target == null) throw new ArgumentNullException(nameof(target));

			context.Annotations.Set(target, ParametersKey, parameters);
		}

		/// <summary>
		///     Gets the protection parameters of the specified target.
		/// </summary>
		/// <param name="context">The context.</param>
		/// <param name="target">The protection target.</param>
		/// <returns>The parameters.</returns>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="context"/> is <see langword="null" />
		///     <br/>- or -<br/>
		///     <paramref name="target"/> is <see langword="null" />
		/// </exception>
		public static ProtectionSettings GetParameters(IConfuserContext context, IDnlibDef target) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (target == null) throw new ArgumentNullException(nameof(target));

			return context.Annotations.GetOrCreate(target, ParametersKey, (t) => new ProtectionSettings());
		}

		/// <summary>
		///     Check if a specific target has already any protection parameters assigned to it.
		/// </summary>
		/// <param name="context">The context.</param>
		/// <param name="target">The protection target.</param>
		/// <returns><see langword="true"/> in case there are parameters; otherwise <see langword="false"/></returns>
		/// <remarks>
		///     Originally this way done by checking if <see cref="GetParameters(IConfuserContext, IDnlibDef)"/>
		///     returned <see langword="null"/>. How ever the behavior of this function was changed any now it always
		///     returns protection parameters by creating them if required.
		/// </remarks>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="context"/> is <see langword="null" />
		///     <br/>- or -<br/>
		///     <paramref name="target"/> is <see langword="null" />
		/// </exception>
		public static bool HasParameters(IConfuserContext context, IDnlibDef target) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (target == null) throw new ArgumentNullException(nameof(target));

			return context.Annotations.Get<ProtectionSettings>(target, ParametersKey) != null;
		}
	}
}
