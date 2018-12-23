using System;
using System.Collections.Immutable;

namespace Confuser.Core.Services {
	internal sealed partial class RuntimeService : IRuntimeService {
		private ImmutableDictionary<string, RuntimeModuleBuilder> _builders =
			ImmutableDictionary.Create<string, RuntimeModuleBuilder>(StringComparer.Ordinal);

		private ImmutableDictionary<string, RuntimeModule> _modules =
			ImmutableDictionary.Create<string, RuntimeModule>(StringComparer.Ordinal);

		IRuntimeModuleBuilder IRuntimeService.CreateRuntimeModule(string name) {
			if (name == null) throw new ArgumentNullException(nameof(name));
			if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name of the runtime module must not be empty or white-space only.", nameof(name));

			if (_builders.ContainsKey(name))
				throw new ArgumentNullException("There is already a runtime module builder with the name " + name);

			var newBuilder = new RuntimeModuleBuilder();
			_builders = _builders.Add(name, newBuilder);

			return newBuilder;
		}

		IRuntimeModule IRuntimeService.GetRuntimeModule(string name) {
			if (name == null) throw new ArgumentNullException(nameof(name));
			if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name of the runtime module must not be empty or white-space only.", nameof(name));

			if (!_modules.TryGetValue(name, out var runtimeModule)) {
				if (!_builders.TryGetValue(name, out var runtimeModuleBuilder))
					throw new ArgumentException("There is no module registered for the name " + name, nameof(name));

				runtimeModule = new RuntimeModule(runtimeModuleBuilder);
				_modules = _modules.Add(name, runtimeModule);
			}
			return runtimeModule;
		}
	}
}
