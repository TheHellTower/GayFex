using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Helpers {
	/// <summary>
	///     Provides methods to inject a <see cref="TypeDef" /> into another module.
	/// </summary>
	public static partial class InjectHelper {
		private static readonly IDictionary<(ModuleDef SourceModule, ModuleDef TargetModule), InjectContext> _contextMap =
			new Dictionary<(ModuleDef SourceModule, ModuleDef TargetModule), InjectContext>();

		private static InjectContext GetOrCreateContext(ModuleDef sourceModule, ModuleDef targetModule) {
			var key = (sourceModule, targetModule);
			if (!_contextMap.TryGetValue(key, out var context)) {
				context = new InjectContext(sourceModule, targetModule);
				_contextMap[key] = context;
			}
			return context;
		}

		public static InjectResult<MethodDef> Inject(MethodDef methodDef, ModuleDef target, IInjectBehavior behavior) {
			if (methodDef == null) throw new ArgumentNullException(nameof(methodDef));
			if (target == null) throw new ArgumentNullException(nameof(target));
			if (behavior == null) throw new ArgumentNullException(nameof(behavior));

			var ctx = GetOrCreateContext(methodDef.Module, target);
			ctx.ApplyMapping(methodDef.DeclaringType, target.GlobalType);
			var injector = new Injector(ctx, behavior);

			var mappedMethod = injector.Inject(methodDef);
			return InjectResult.Create(methodDef, mappedMethod, injector.InjectedMembers.Where(kvp => kvp.Key != methodDef));
		}
	}
}
