using System.Collections;
using System.Collections.Generic;
using dnlib.DotNet;

namespace Confuser.Helpers {
	public sealed class InjectResult<T> : IEnumerable<(IMemberDef Source, IMemberDef Mapped)> where T : IMemberDef {
		public (T Source, T Mapped) Requested { get; }
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
