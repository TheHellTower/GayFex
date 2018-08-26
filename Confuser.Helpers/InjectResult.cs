using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using dnlib.DotNet;

namespace Confuser.Helpers {
	internal static class InjectResult {

		internal static InjectResult<T> Create<T>(T source, T mapped) where T : IMemberDef =>
			new InjectResult<T>(source, mapped, ImmutableArray.Create<(IMemberDef, IMemberDef)>());

		internal static InjectResult<T> Create<T>(T source, T mapped, IEnumerable<KeyValuePair<IMemberDef, IMemberDef>> dependencies) where T : IMemberDef =>
			new InjectResult<T>(source, mapped, dependencies.Select(kvp => (kvp.Key, kvp.Value)).ToImmutableList());
	}
}
