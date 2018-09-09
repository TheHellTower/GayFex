using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace Confuser.Helpers {
	internal static class InjectResult {

		internal static InjectResult<T> Create<T>(T source, T mapped) where T : IMemberDef =>
			new InjectResult<T>(source, mapped, ImmutableArray.Create<(IMemberDef, IMemberDef)>());

		internal static InjectResult<T> Create<T>(T source, T mapped, IEnumerable<KeyValuePair<IMemberDef, IMemberDef>> dependencies) where T : IMemberDef {
#if DEBUG
			if (mapped is MethodDef mappedMethod && mappedMethod.HasBody) {
				Debug.Assert(MaxStackCalculator.GetMaxStack(mappedMethod.Body.Instructions, mappedMethod.Body.ExceptionHandlers, out var maxStack),
					"Calculating the stack size of the injected method failed. Something is wrong!");
			}
			foreach (var dep in dependencies) {
				if (dep.Value is MethodDef depMethod && depMethod.HasBody) {
					Debug.Assert(MaxStackCalculator.GetMaxStack(depMethod.Body.Instructions, depMethod.Body.ExceptionHandlers, out var maxStack),
						"Calculating the stack size of the injected method failed. Something is wrong!");
				}
			}
#endif

			return new InjectResult<T>(source, mapped, dependencies.Select(kvp => (kvp.Key, kvp.Value)).ToImmutableList());
		}
	}
}
