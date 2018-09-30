using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Helpers;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	internal sealed class AntiDumpProtectionPhase : IProtectionPhase {
		public AntiDumpProtectionPhase(AntiDumpProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public AntiDumpProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Modules;

		public string Name => "Anti-dump injection";

		public bool ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			var runtime = context.Registry.GetRequiredService<ProtectionsRuntimeService>().GetRuntimeModule();
			var marker = context.Registry.GetRequiredService<IMarkerService>();
			var logger = context.Registry.GetRequiredService<ILoggingService>().GetLogger(nameof(AntiDumpProtectionPhase));

			foreach (var module in parameters.Targets.OfType<ModuleDef>()) {
				var initMethod = GetInitMethod(module, context, runtime, logger);
				if (initMethod == null) continue;

				var injectResult = InjectHelper.Inject(initMethod, module, InjectBehaviors.RenameAndNestBehavior(context, module.GlobalType));

				var cctor = module.GlobalType.FindStaticConstructor();
				cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, injectResult.Requested.Mapped));

				foreach (var dependencies in injectResult.InjectedDependencies)
					marker.Mark(context, dependencies.Mapped, Parent);
			}
		}

		private static MethodDef GetInitMethod(ModuleDef module, IConfuserContext context, IRuntimeModule runtimeModule, Core.ILogger logger) {
			Debug.Assert(module != null, $"{nameof(module)} != null");
			Debug.Assert(context != null, $"{nameof(context)} != null");
			Debug.Assert(runtimeModule != null, $"{nameof(runtimeModule)} != null");
			Debug.Assert(logger != null, $"{nameof(logger)} != null");

			const string runtimeTypeName = "Confuser.Runtime.AntiDump";

			TypeDef rtType = null;
			try {
				rtType = runtimeModule.GetRuntimeType(runtimeTypeName, module);
			}
			catch (ArgumentException ex) {
				logger.Error("Failed to load runtime: " + ex.Message);
				return null;
			}

			if (rtType == null) {
				logger.Error("Failed to load runtime: " + runtimeTypeName);
				return null;
			}

			var initMethod = rtType.FindMethod("Initialize");
			if (initMethod == null) {
				logger.Error("Could not find \"Initialize\" for " + rtType.FullName);
				return null;
			}

			return initMethod;
		}
	}
}
