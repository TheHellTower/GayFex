using System;

namespace Confuser.Core.Services
{
	public interface IRandomGenerator {
		/// <summary>
		///     Fills the specified buffer with random bytes.
		/// </summary>
		/// <param name="buffer">The buffer.</param>
		void NextBytes(Span<byte> buffer);

		/// <summary>
		///     Returns a random byte.
		/// </summary>
		/// <returns>Requested random byte.</returns>
		byte NextByte();

		/// <summary>
		///     Returns a random boolean value.
		/// </summary>
		/// <returns>Requested random boolean value.</returns>
		bool NextBoolean();
	}
}
