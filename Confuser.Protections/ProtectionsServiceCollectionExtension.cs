using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Confuser.Protections {
	internal static class ProtectionsServiceCollectionExtension {
		internal static IServiceCollection AddRuntime(this IServiceCollection services) {
			if (services == null) throw new ArgumentNullException(nameof(services));

			services.TryAdd(ServiceDescriptor.Singleton(p => new ProtectionsRuntimeService(p)));

			return services;
		}
	}
}
