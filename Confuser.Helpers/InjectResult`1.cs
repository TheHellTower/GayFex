using System.Collections.Generic;
using dnlib.DotNet;

namespace Confuser.Helpers {
	public sealed class InjectResult<T> where T : IMemberDef {
		public (T Source, T Mapped) Requested { get; }
		public IReadOnlyCollection<(IMemberDef Source, IMemberDef Mapped)> InjectedDependencies { get; }

		internal InjectResult(T source, T mapped, IReadOnlyCollection<(IMemberDef, IMemberDef)> dependencies) {
			Requested = (source, mapped);
			InjectedDependencies = dependencies;
		}
	}
}
