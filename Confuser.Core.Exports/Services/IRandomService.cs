namespace Confuser.Core.Services {
	/// <summary>
	///     Provides methods to obtain a unique stable PRNG for any given ID.
	/// </summary>
	public interface IRandomService {
		/// <summary>
		///     Gets a RNG with the specified identifier.
		/// </summary>
		/// <param name="id">The identifier.</param>
		/// <returns>The requested RNG.</returns>
		/// <exception cref="System.ArgumentNullException"><paramref name="id" /> is <see langword="null"/>.</exception>
		IRandomGenerator GetRandomGenerator(string id);

		string SeedString { get; }
	}
}
