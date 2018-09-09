using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
		private static Stack<IImmutableDictionary<(ModuleDef SourceModule, ModuleDef TargetModule), InjectContext>> _parentMaps =
			new Stack<IImmutableDictionary<(ModuleDef SourceModule, ModuleDef TargetModule), InjectContext>>();

		private static IImmutableDictionary<(ModuleDef SourceModule, ModuleDef TargetModule), InjectContext> _contextMap =
			ImmutableDictionary.Create<(ModuleDef SourceModule, ModuleDef TargetModule), InjectContext>();

		private static InjectContext GetOrCreateContext(ModuleDef sourceModule, ModuleDef targetModule) {
			var key = (sourceModule, targetModule);
			// Check if the current map has a context for the two modules.
			if (!_contextMap.TryGetValue(key, out var context)) {
				// Check if there is an known context on the parent map.
				if (!_parentMaps.Any() || !_parentMaps.Peek().TryGetValue(key, out context)) {
					// Also the parent context knows nothing about this context. So there really is known.
					context = new InjectContext(sourceModule, targetModule);
				}
				else {
					// We got a context on the parent. This means we need to create a child context that covers all
					// injects for the current injection block.
					context = new InjectContext(context);
				}
				_contextMap = _contextMap.Add(key, context);
			}
			return context;
		}

		public static IDisposable CreateChildContext() {
			var parentMap = _contextMap;
			if (_parentMaps.Any()) {
				var oldParentMap = _parentMaps.Peek();
				if (parentMap.Any()) {
					foreach (var kvp in oldParentMap) {
						if (!parentMap.ContainsKey(kvp.Key))
							parentMap = parentMap.Add(kvp.Key, kvp.Value);
					}
				} 
				else {
					parentMap = oldParentMap;
				}
			}
			_parentMaps.Push(parentMap);
			_contextMap = _contextMap.Clear();

			return new ChildContextRelease(ReleaseChildContext);
		}

		private static void ReleaseChildContext() {
			if (!_parentMaps.Any()) throw new InvalidOperationException("There is not child context to release. Disposed twice?!");

			_contextMap = _parentMaps.Pop();
		}

		public static InjectResult<MethodDef> Inject(MethodDef methodDef,
			                                         ModuleDef target, 
													 IInjectBehavior behavior, 
													 params IMethodInjectProcessor[] methodInjectProcessors) {
			if (methodDef == null) throw new ArgumentNullException(nameof(methodDef));
			if (target == null) throw new ArgumentNullException(nameof(target));
			if (behavior == null) throw new ArgumentNullException(nameof(behavior));
			if (methodInjectProcessors == null) throw new ArgumentNullException(nameof(methodInjectProcessors));

			var ctx = GetOrCreateContext(methodDef.Module, target);
			ctx.ApplyMapping(methodDef.DeclaringType, target.GlobalType);
			var injector = new Injector(ctx, behavior, methodInjectProcessors);

			var mappedMethod = injector.Inject(methodDef);
			return InjectResult.Create(methodDef, mappedMethod, injector.InjectedMembers.Where(kvp => kvp.Key != methodDef));
		}

		private sealed class ChildContextRelease : IDisposable {
			private readonly Action _releaseAction;
			private bool _disposed = false;

			internal ChildContextRelease(Action releaseAction) {
				Debug.Assert(releaseAction != null, $"{nameof(releaseAction)} != null");

				_releaseAction = releaseAction;
			}

			void Dispose(bool disposing) {
				if (!_disposed) {
					if (disposing) {

					}
					_disposed = true;
				}
			}
			
			void IDisposable.Dispose() {
				Dispose(true);
				GC.SuppressFinalize(this);
			}
		}
	}
}
