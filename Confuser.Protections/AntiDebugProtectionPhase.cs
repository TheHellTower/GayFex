using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Helpers;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Protections {
	internal sealed class AntiDebugProtectionPhase : IProtectionPhase {
		public AntiDebugProtectionPhase(AntiDebugProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public AntiDebugProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Modules;

		public string Name => "Anti-debug injection";

		public bool ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters,
			CancellationToken token) {
			var rtService = context.Registry.GetRequiredService<ProtectionsRuntimeService>();
			var rt = rtService.GetRuntimeModule();
			var marker = context.Registry.GetRequiredService<IMarkerService>();
			var name = context.Registry.GetRequiredService<INameService>();
			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(AntiDebugProtection._Id);

			foreach (var module in parameters.Targets.OfType<ModuleDef>()) {
				var initMethod = GetInitMethod(module, context, parameters, rt, logger);
				if (initMethod == null) continue;

				var injectResult = rtService.InjectHelper.Inject(initMethod, module,
					InjectBehaviors.RenameAndNestBehavior(context, module.GlobalType));
				var cctor = module.GlobalType.FindStaticConstructor();
				cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, injectResult.Requested.Mapped));

				foreach (var dependencies in injectResult.InjectedDependencies)
					marker.Mark(context, dependencies.Mapped, Parent);
			}
		}

		private MethodDef GetInitMethod(ModuleDef module, IConfuserContext context, IProtectionParameters parameters,
			IRuntimeModule runtimeModule, ILogger logger) {
			Debug.Assert(module != null, $"{nameof(module)} != null");
			Debug.Assert(context != null, $"{nameof(context)} != null");
			Debug.Assert(parameters != null, $"{nameof(parameters)} != null");
			Debug.Assert(runtimeModule != null, $"{nameof(runtimeModule)} != null");
			Debug.Assert(logger != null, $"{nameof(logger)} != null");

			var mode = parameters.GetParameter(context, module, Parent.Parameters.Mode);

			string runtimeTypeName = null;
			switch (mode) {
				case AntiDebugMode.Safe:
					runtimeTypeName = "Confuser.Runtime.AntiDebugSafe";
					break;
				case AntiDebugMode.Win32:
					runtimeTypeName = "Confuser.Runtime.AntiDebugWin32";
					break;
				case AntiDebugMode.Antinet:
					runtimeTypeName = "Confuser.Runtime.AntiDebugAntinet";
					break;
				default:
					throw new UnreachableException();
			}

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
