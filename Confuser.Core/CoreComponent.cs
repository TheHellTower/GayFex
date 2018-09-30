using Confuser.Core.API;
using Confuser.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Core {
	/// <summary>
	///     Core component of Confuser.
	/// </summary>
	internal sealed class CoreComponent : IConfuserComponent {

		readonly Marker marker;
		readonly ConfuserParameters parameters;

		/// <summary>
		///     Initializes a new instance of the <see cref="CoreComponent" /> class.
		/// </summary>
		/// <param name="parameters">The parameters.</param>
		/// <param name="marker">The marker.</param>
		internal CoreComponent(ConfuserParameters parameters, Marker marker) {
			this.parameters = parameters;
			this.marker = marker;
		}

		/// <inheritdoc />
		public string Name => "Confuser Core";

		/// <inheritdoc />
		public string Description => "Initialization of Confuser core services.";

		/// <inheritdoc />
		void IConfuserComponent.Initialize(IServiceCollection collection) {
			collection.AddTransient(typeof(IPackerService), (p) => new PackerService(p));
			collection.AddSingleton(typeof(ILoggingService), (p) => new LoggingService(parameters.Logger));
			collection.AddSingleton(typeof(IRandomService), (p) => new RandomService(parameters.Project.Seed));
			collection.AddSingleton(typeof(IMarkerService), (p) => new MarkerService(marker));
			collection.AddSingleton(typeof(ITraceService), (p) => new TraceService());
			collection.AddSingleton(typeof(IRuntimeService), (p) => new RuntimeService());
			collection.AddSingleton(typeof(ICompressionService), (p) => new CompressionService(p));
			collection.AddSingleton(typeof(IAPIStore), (p) => new APIStore(p));

			collection.AddSingleton(p => new CoreRuntimeService(p));
		}

		/// <inheritdoc />
		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) { }
	}
}
