using System;
using System.Collections.Generic;
using System.Diagnostics;
using dnlib.DotNet;

namespace Confuser.Helpers {
	public static partial class InjectHelper {
		/// <summary>
		///     The inject context is used to store what definitions were injected from one module into another.
		/// </summary>
		private sealed class InjectContext : ImportResolver {
			/// <summary>
			///     The mapping of origin definitions to injected definitions.
			/// </summary>
			private readonly Dictionary<IMemberDef, IMemberDef> _map;

			/// <summary>
			///     The module which source type originated from.
			/// </summary>
			internal ModuleDef OriginModule { get; }

			/// <summary>
			///     The module which source type is being injected to.
			/// </summary>
			internal ModuleDef TargetModule { get; }

			/// <summary>
			///     Initializes a new instance of the <see cref="InjectContext" /> class.
			/// </summary>
			/// <param name="module">The origin module.</param>
			/// <param name="target">The target module.</param>
			internal InjectContext(ModuleDef module, ModuleDef target) {
				OriginModule = module ?? throw new ArgumentNullException(nameof(module));
				TargetModule = target ?? throw new ArgumentNullException(nameof(target));

				_map = new Dictionary<IMemberDef, IMemberDef>();
			}

			internal void ApplyMapping(IMemberDef source, IMemberDef target) {
				if (source == null) throw new ArgumentNullException(nameof(source));
				if (target == null) throw new ArgumentNullException(nameof(target));

				Debug.Assert(!_map.ContainsKey(source));
				_map[source] = target;
			}

			internal TDef ResolveMapped<TDef>(TDef def) where TDef : class, IMemberDef {
				if (_map.TryGetValue(def, out var mappedDef) && mappedDef is TDef resultDef)
					return resultDef;
				return null;
			}
		}
	}
}
