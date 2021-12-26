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
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Protections {
	internal sealed class AntiDumpProtectionPhase : IProtectionPhase {
		public AntiDumpProtectionPhase(AntiDumpProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public AntiDumpProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Modules;

		public string Name => "Anti-dump injection";

		public bool ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters,
			CancellationToken token) {
			var runtimeService = context.Registry.GetRequiredService<ProtectionsRuntimeService>();
			var runtime = runtimeService.GetRuntimeModule();
			var marker = context.Registry.GetRequiredService<IMarkerService>();
			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(AntiDumpProtection._Id);

			foreach (var module in parameters.Targets.OfType<ModuleDef>()) {
				var initMethod = GetInitMethod(module, context, runtime, logger);
				if (initMethod == null) continue;

				var injectResult = runtimeService.InjectHelper.Inject(initMethod, module,
					InjectBehaviors.RenameAndNestBehavior(context, module.GlobalType));

				var cctor = module.GlobalType.FindStaticConstructor();
				cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, injectResult.Requested.Mapped));

				foreach (var dependencies in injectResult.InjectedDependencies)
					marker.Mark(context, dependencies.Mapped, Parent);
			}
		}

		private static MethodDef GetInitMethod(ModuleDef module, IConfuserContext context, IRuntimeModule runtimeModule,
			ILogger logger) {
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
				logger.LogError("Failed to load runtime: {0}", ex.Message);
				return null;
			}

			if (rtType == null) {
				logger.LogError("Failed to load runtime: {0}", runtimeTypeName);
				return null;
			}

			var initMethod = rtType.FindMethod("Initialize");
			if (initMethod == null) {
				logger.LogError("Could not find \"Initialize\" for {0}", rtType.FullName);
				return null;
			}

			return initMethod;
		}
	}
}
