using System.Collections;
using System.Collections.Generic;
using dnlib.DotNet;

namespace Confuser.Helpers {
	/// <summary>
	///     The result of the injection.
	///     Provides the mapping of the requested member and all injected dependencies.
	/// </summary>
	/// <typeparam name="T">The type of the requested member</typeparam>
	/// <remarks>
	///     The iterating over this object provides all injected members stored in this result.
	///     Includes the requested member and all dependencies.
	/// </remarks>
	public sealed class InjectResult<T> : IEnumerable<(IMemberDef Source, IMemberDef Mapped)> where T : IMemberDef {
		/// <summary>The mapping of the requested member.</summary>
		public (T Source, T Mapped) Requested { get; }

		/// <summary>The mapping of all dependencies.</summary>
		/// <remarks>This does not contain the mapping of the requested mapping.</remarks>
		public IReadOnlyCollection<(IMemberDef Source, IMemberDef Mapped)> InjectedDependencies { get; }

		internal InjectResult(T source, T mapped, IReadOnlyCollection<(IMemberDef, IMemberDef)> dependencies) {
			Requested = (source, mapped);
			InjectedDependencies = dependencies;
		}

		private IEnumerable<(IMemberDef, IMemberDef)> GetAllMembers() {
			yield return Requested;
			foreach (var dep in InjectedDependencies)
				yield return dep;
		}

		IEnumerator<(IMemberDef Source, IMemberDef Mapped)> IEnumerable<(IMemberDef Source, IMemberDef Mapped)>.GetEnumerator() =>
			GetAllMembers().GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetAllMembers().GetEnumerator();
	}
}
