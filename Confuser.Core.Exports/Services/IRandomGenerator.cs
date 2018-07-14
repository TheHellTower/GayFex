using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Confuser.Core.Services
{
	public interface IRandomGenerator {
		/// <summary>
		///     Fills the specified buffer with random bytes.
		/// </summary>
		/// <param name="buffer">The buffer.</param>
		/// <param name="offset">The offset of buffer to fill in.</param>
		/// <param name="length">The number of random bytes.</param>
		/// <exception cref="ArgumentNullException"><paramref name="buffer" /> is <c>null</c>.</exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///     <paramref name="offset" /> or <paramref name="length" /> is less than 0.
		/// </exception>
		/// <exception cref="ArgumentException">Invalid <paramref name="offset" /> or <paramref name="length" />.</exception>
		void NextBytes(byte[] buffer, int offset, int length);

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
