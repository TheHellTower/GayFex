using System;
using System.Globalization;
using System.Linq;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal partial class RegexCompiler {
		private sealed class Mapper : ImportMapper {
			private IConfuserContext Context { get; }
			private ModuleDef TargetModule { get; }
			private RegexRunnerDef RunnerDef { get; }

			internal Mapper(IConfuserContext context, ModuleDef targetModule, RegexRunnerDef runnerDef) {
				Context = context ?? throw new ArgumentNullException(nameof(context));
				TargetModule = targetModule ?? throw new ArgumentNullException(nameof(targetModule));
				RunnerDef = runnerDef ?? throw new ArgumentNullException(nameof(runnerDef));
			}

			public override TypeRef Map(Type source) {
				var mappedRef = MapReference(source.Namespace, source.FullName);
				return mappedRef ?? base.Map(source);
			}

			private TypeRef MapReference(string ns, string fullname) {
				// First check if it's the Regex assembly.
				// If so, we know that the module is present in the target. Just import it.
				if (string.Equals(ns, CompileRegexProtection._RegexNamespace, StringComparison.Ordinal))
					return TargetModule.Import(RunnerDef.RegexModule.FindThrow(fullname, false));

				// Second try. Check all the already present type references for a match. If any is present, we can use it.
				var existingRef = TargetModule.GetTypeRefs().FirstOrDefault(tr =>
					string.Equals(tr.FullName, fullname, StringComparison.Ordinal));
				if (!(existingRef is null)) return existingRef;

				// Third round. Check the references of the regex module. Maybe we can borrow something there.
				var regexModRef = RunnerDef.RegexModule.GetTypeRefs().FirstOrDefault(tr =>
					string.Equals(tr.FullName, fullname, StringComparison.Ordinal));
				if (!(regexModRef is null)) return TargetModule.Import(regexModRef.ResolveThrow());

				// Now it's getting difficult. Check all the assemblies that are currently referenced by the target module.
				// This is the last chance we got.
				foreach (var moduleDef in TargetModule.GetAssemblyRefs().Select(a => Context.Resolver.ResolveThrow(a, TargetModule)).SelectMany(a => a.Modules)) {
					var referencedType = moduleDef.Find(fullname, false);
					if (!(referencedType is null))
						return TargetModule.Import(referencedType);
				}

				// We got nothing. Bailing out.
				return null;
			}
		}
	}
}
