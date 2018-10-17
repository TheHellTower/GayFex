using System;
using Confuser.Core;
using Confuser.Core.Services;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Confuser.Protections.AntiTamper {
	internal static class ModeHandlerRuntime {
		internal static MethodDef GetInitMethod(this IConfuserContext context, string runtimeTypeFullName, ModuleDef targetModule) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (runtimeTypeFullName == null) throw new ArgumentNullException(nameof(runtimeTypeFullName));
			if (targetModule == null) throw new ArgumentNullException(nameof(targetModule));

			var runtime = context.Registry.GetRequiredService<ProtectionsRuntimeService>().GetRuntimeModule();
			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(AntiTamperProtection._Id);

			TypeDef rtType = null;
			try {
				rtType = runtime.GetRuntimeType(runtimeTypeFullName, targetModule);
			}
			catch (ArgumentException ex) {
				logger.LogError("Failed to load runtime: {0}", ex.Message);
				return null;
			}

			if (rtType == null) {
				logger.LogError("Failed to load runtime: {0}", runtimeTypeFullName);
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
