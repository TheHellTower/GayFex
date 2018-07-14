using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Core {
	/// <summary>
	///     Represent a component in Confuser
	/// </summary>
	public interface IConfuserComponent {
		/// <summary>
		///     Gets the name of component.
		/// </summary>
		/// <value>The name of component.</value>
		string Name { get; }

		/// <summary>
		///     Gets the description of component.
		/// </summary>
		/// <value>The description of component.</value>
		string Description { get; }

		/// <summary>
		///     Gets the identifier of component used by users.
		/// </summary>
		/// <value>The identifier of component.</value>
		string Id { get; }

		/// <summary>
		///     Gets the full identifier of component used in Confuser.
		/// </summary>
		/// <value>The full identifier of component.</value>
		string FullId { get; }

		/// <summary>
		///     Initializes the component.
		/// </summary>
		/// <param name="collection">The collection used to register any required services.</param>
		void Initialize(IServiceCollection collection);

		/// <summary>
		///     Inserts protection stages into processing pipeline.
		/// </summary>
		/// <param name="pipeline">The processing pipeline.</param>
		void PopulatePipeline(IProtectionPipeline pipeline);
	}
}
