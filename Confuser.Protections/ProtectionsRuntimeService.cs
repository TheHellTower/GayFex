using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	internal sealed class ProtectionsRuntimeService {
		private const string RuntimeModuleName = "Confuser.Protections";
		private const string RuntimeResourceIdentifier = "Confuser.Protections.Confuser.Protections.Runtime.";
		private const string RuntimeResourceExtension = ".dll";

		private IRuntimeService RuntimeService { get; }

		internal InjectHelper InjectHelper { get; }

		internal ProtectionsRuntimeService(IServiceProvider provider) {
			if (provider == null) throw new ArgumentNullException(nameof(provider));

			InjectHelper = new InjectHelper(provider);

			var runtimeService = provider.GetRequiredService<IRuntimeService>();
			RuntimeService = runtimeService;
			var builder = runtimeService.CreateRuntimeModule(RuntimeModuleName);

			var assembly = typeof(ProtectionsRuntimeService).Assembly;
			var manifestResourceNames = assembly.GetManifestResourceNames();
			foreach (var resourceName in manifestResourceNames.Where(IsRuntimeDll)) {
				Debug.Assert(resourceName != null, nameof(resourceName) + " != null");

				Stream AssemblyStreamFactory() => assembly.GetManifestResourceStream(resourceName);
				Func<Stream> symbolStreamFactory = null;

				var symbolManifestResourceName = Path.ChangeExtension(resourceName, ".pdb");
				if (manifestResourceNames.Contains(symbolManifestResourceName))
					symbolStreamFactory = () =>
						assembly.GetManifestResourceStream(Path.ChangeExtension(resourceName, ".pdb"));

				var frameworkIdentifier = resourceName.Substring(RuntimeResourceIdentifier.Length,
					resourceName.Length - RuntimeResourceIdentifier.Length - RuntimeResourceExtension.Length);

				builder.AddImplementation(frameworkIdentifier, AssemblyStreamFactory, symbolStreamFactory);
			}
		}

		internal IRuntimeModule GetRuntimeModule() => RuntimeService.GetRuntimeModule(RuntimeModuleName);

		private static bool IsRuntimeDll(string resourceName) =>
			resourceName != null &&
			resourceName.StartsWith(RuntimeResourceIdentifier, StringComparison.Ordinal) &&
			resourceName.EndsWith(RuntimeResourceExtension, StringComparison.Ordinal) &&
			resourceName.Length > RuntimeResourceIdentifier.Length + RuntimeResourceExtension.Length;
	}
}
