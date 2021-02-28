using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Confuser.Optimizations {
	internal static class OptimizationsServiceCollectionExtension {
		internal static IServiceCollection AddRuntime(this IServiceCollection services) {
			if (services == null) throw new ArgumentNullException(nameof(services));

			services.TryAdd(ServiceDescriptor.Singleton(p => new OptimizationsRuntimeService(p)));

			return services;
		}
	}
}
