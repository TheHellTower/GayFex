using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Core.Services {
	internal sealed class CoreRuntimeService {
		private const string RuntimeModuleName = "Confuser.Core";
		private const string RuntimeResourceIdentifer = "Confuser.Core.Confuser.Core.Runtime.";
		private const string RuntimeResourceExtension = ".dll";

		private IRuntimeService RuntimeService { get; }

		internal CoreRuntimeService(IServiceProvider provider) {
			if (provider == null) throw new ArgumentNullException(nameof(provider));

			var runtimeService = provider.GetRequiredService<IRuntimeService>();
			RuntimeService = runtimeService;
			var builder = runtimeService.CreateRuntimeModule(RuntimeModuleName);

			var assembly = typeof(CoreRuntimeService).Assembly;
			var manifestResourceNames = assembly.GetManifestResourceNames();
			foreach (var resourceName in manifestResourceNames.Where(IsRuntimeDll)) {
				var localResName = resourceName;
				Func<Stream> assemblyStreamFactory = () => assembly.GetManifestResourceStream(resourceName);
				Func<Stream> symbolStreamFactory = null;

				var symbolManifestResourceName = Path.ChangeExtension(resourceName, ".pdb");
				if (manifestResourceNames.Contains(symbolManifestResourceName))
					symbolStreamFactory = () => assembly.GetManifestResourceStream(Path.ChangeExtension(resourceName, ".pdb"));

				var frameworkIdentifier = resourceName.Substring(RuntimeResourceIdentifer.Length,
					resourceName.Length - RuntimeResourceIdentifer.Length - RuntimeResourceExtension.Length);

				builder.AddImplementation(frameworkIdentifier, assemblyStreamFactory, symbolStreamFactory);
			}
		}

		internal IRuntimeModule GetRuntimeModule() => RuntimeService.GetRuntimeModule(RuntimeModuleName);

		private static bool IsRuntimeDll(string resourceName) =>
			resourceName.StartsWith(RuntimeResourceIdentifer) && resourceName.EndsWith(RuntimeResourceExtension);
	}
}
