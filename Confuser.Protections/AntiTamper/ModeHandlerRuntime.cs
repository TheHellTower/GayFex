using System;
using Confuser.Core;
using Confuser.Core.Services;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.AntiTamper {
	internal static class ModeHandlerRuntime {
		internal static MethodDef GetInitMethod(this IConfuserContext context, string runtimeTypeFullName, ModuleDef targetModule) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (runtimeTypeFullName == null) throw new ArgumentNullException(nameof(runtimeTypeFullName));
			if (targetModule == null) throw new ArgumentNullException(nameof(targetModule));

			var runtime = context.Registry.GetRequiredService<ProtectionsRuntimeService>().GetRuntimeModule();
			var logger = context.Registry.GetRequiredService<ILoggingService>().GetLogger(nameof(AntiTamperProtection));

			TypeDef rtType = null;
			try {
				rtType = runtime.GetRuntimeType(runtimeTypeFullName, targetModule);
			}
			catch (ArgumentException ex) {
				logger.Error("Failed to load runtime: " + ex.Message);
				return null;
			}

			if (rtType == null) {
				logger.Error("Failed to load runtime: " + runtimeTypeFullName);
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
