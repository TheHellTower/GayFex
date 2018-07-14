using System;
using Confuser.Core.API;
using Confuser.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Core {
	/// <summary>
	///     Core component of Confuser.
	/// </summary>
	internal sealed class CoreComponent : IConfuserComponent {
		/// <summary>
		///     The service ID of RNG
		/// </summary>
		public const string _RandomServiceId = "Confuser.Random";

		/// <summary>
		///     The service ID of Marker
		/// </summary>
		public const string _MarkerServiceId = "Confuser.Marker";

		/// <summary>
		///     The service ID of Trace
		/// </summary>
		public const string _TraceServiceId = "Confuser.Trace";

		/// <summary>
		///     The service ID of Runtime
		/// </summary>
		public const string _RuntimeServiceId = "Confuser.Runtime";

		/// <summary>
		///     The service ID of Compression
		/// </summary>
		public const string _CompressionServiceId = "Confuser.Compression";

		/// <summary>
		///     The service ID of API Store
		/// </summary>
		public const string _APIStoreId = "Confuser.APIStore";

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
		public string Id => "Confuser.Core";

		/// <inheritdoc />
		public string FullId => "Confuser.Core";

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
		}

		/// <inheritdoc />
		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) { }
	}
}
