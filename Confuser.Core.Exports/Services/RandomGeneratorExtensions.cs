using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using dnlib.DotNet.Writer;

namespace Confuser.Core.Services {
	/// <summary>
	/// These are the extension methods for the random value generator. They are the same no matter the implementation
	/// of the <see cref="IRandomGenerator"/> that is used.
	/// </summary>
	public static class RandomGeneratorExtensions {
		/// <summary>
		///     Gets a buffer of random bytes with the specified length.
		/// </summary>
		/// <param name="generator">The generator used to generate the values.</param>
		/// <param name="length">The number of random bytes.</param>
		/// <returns>A buffer of random bytes.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="generator"/> is <see langword="null" /></exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="generator"/> &lt; 0</exception>
		public static Memory<byte> NextBytes(this IRandomGenerator generator, int length) {
			if (generator == null) throw new ArgumentNullException(nameof(generator));
			if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), length, "Length can't be less than 0");

			if (length == 0) return Memory<byte>.Empty;

			Memory<byte> ret = new byte[length];
			generator.NextBytes(ret.Span);
			return ret;
		}

		/// <summary>
		///     Returns a random signed integer.
		/// </summary>
		/// <param name="generator">The generator used to generate the values.</param>
		/// <returns>Requested random number.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="generator"/> is <see langword="null" /></exception>
		public static int NextInt32(this IRandomGenerator generator) {
			if (generator == null) throw new ArgumentNullException(nameof(generator));

			Span<byte> buffer = stackalloc byte[4];
			generator.NextBytes(buffer);
			var tmpArray = ArrayPool<byte>.Shared.Rent(4);
			try {
				buffer.CopyTo(tmpArray);
				return BitConverter.ToInt32(tmpArray, 0);
			} finally {
				ArrayPool<byte>.Shared.Return(tmpArray);
			}
		}

		/// <summary>
		///     Returns a nonnegative random integer that is less than the specified maximum.
		/// </summary>
		/// <param name="generator">The generator used to generate the values.</param>
		/// <param name="max">The exclusive upper bound.</param>
		/// <returns>Requested random number.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="generator"/> is <see langword="null" /></exception>
		public static int NextInt32(this IRandomGenerator generator, int max) => (int)(NextUInt32(generator) % max);

		/// <summary>
		///     Returns a random integer that is within a specified range.
		/// </summary>
		/// <param name="generator">The generator used to generate the values.</param>
		/// <param name="min">The inclusive lower bound.</param>
		/// <param name="max">The exclusive upper bound.</param>
		/// <returns>Requested random number.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="generator"/> is <see langword="null" /></exception>
		public static int NextInt32(this IRandomGenerator generator, int min, int max) {
			if (max <= min) return min;
			return min + NextInt32(generator, max - min);
		}

		/// <summary>
		///     Returns a random unsigned integer.
		/// </summary>
		/// <param name="generator">The generator used to generate the values.</param>
		/// <returns>Requested random number.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="generator"/> is <see langword="null" /></exception>
		public static uint NextUInt32(this IRandomGenerator generator) {
			if (generator == null) throw new ArgumentNullException(nameof(generator));

			Span<byte> buffer = stackalloc byte[4];
			generator.NextBytes(buffer);
			var tmpArray = ArrayPool<byte>.Shared.Rent(4);
			try {
				buffer.CopyTo(tmpArray);
				return BitConverter.ToUInt32(tmpArray, 0);
			}
			finally {
				ArrayPool<byte>.Shared.Return(tmpArray);
			}
		}

		/// <summary>
		///     Returns a nonnegative random integer that is less than the specified maximum.
		/// </summary>
		/// <param name="generator">The generator used to generate the values.</param>
		/// <param name="max">The exclusive upper bound.</param>
		/// <returns>Requested random number.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="generator"/> is <see langword="null" /></exception>
		public static uint NextUInt32(this IRandomGenerator generator, uint max) => NextUInt32(generator) % max;

		/// <summary>
		///     Returns a nonnegative random integer that is within a specified range.
		/// </summary>
		/// <param name="generator">The generator used to generate the values.</param>
		/// <param name="min">The inclusive lower bound.</param>
		/// <param name="max">The exclusive upper bound.</param>
		/// <returns>Requested random number.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="generator"/> is <see langword="null" /></exception>
		public static uint NextUInt32(this IRandomGenerator generator, uint min, uint max) {
			if (max <= min) return min;
			return min + NextUInt32(generator, max - min);
		}

		/// <summary>
		///     Returns a random double floating pointer number from 0 (inclusive) to 1 (exclusive).
		/// </summary>
		/// <param name="generator">The generator used to generate the values.</param>
		/// <returns>Requested random number.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="generator"/> is <see langword="null" /></exception>
		public static double NextDouble(this IRandomGenerator generator) =>
			NextUInt32(generator) / ((double)uint.MaxValue + 1);

		/// <summary>
		///     Shuffles the element in the specified list.
		/// </summary>
		/// <typeparam name="T">The element type of the list.</typeparam>
		/// <param name="generator">The generator used to generate the values.</param>
		/// <param name="list">The list to shuffle.</param>
		/// <exception cref="ArgumentNullException">Any parameter is <see langword="null" /></exception>
		public static void Shuffle<T>(this IRandomGenerator generator, IList<T> list) {
			if (generator == null) throw new ArgumentNullException(nameof(generator));
			if (list == null) throw new ArgumentNullException(nameof(list));

			if (!list.Any()) return;

			for (int i = list.Count - 1; i > 1; i--) {
				int k = NextInt32(generator, i + 1);
				var tmp = list[k];
				list[k] = list[i];
				list[i] = tmp;
			}
		}

		/// <summary>
		///     Create a new list with the elements of the <paramref name="list"/> in random order.
		/// </summary>
		/// <typeparam name="T">The element type of the list.</typeparam>
		/// <param name="generator">The generator used to generate the values.</param>
		/// <param name="list">The list to shuffle.</param>
		/// <returns>The new list with the shuffled elements.</returns>
		/// <exception cref="ArgumentNullException">Any parameter is <see langword="null" /></exception>
		public static IImmutableList<T> Shuffle<T>(this IRandomGenerator generator, IImmutableList<T> list) {
			if (generator == null) throw new ArgumentNullException(nameof(generator));
			if (list == null) throw new ArgumentNullException(nameof(list));

			var builder = ImmutableArray.CreateBuilder<T>(list.Count);
			builder.AddRange(list);
			Shuffle(generator, builder);
			return builder.MoveToImmutable();
		}

		/// <summary>
		///     Shuffles the element in the specified metadata table.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="generator">The generator used to generate the values.</param>
		/// <param name="table">The metadata table to shuffle.</param>
		/// <exception cref="ArgumentNullException">Any parameter is <see langword="null" /></exception>
		public static void Shuffle<T>(this IRandomGenerator generator, MDTable<T> table) where T : struct {
			if (generator == null) throw new ArgumentNullException(nameof(generator));
			if (table == null) throw new ArgumentNullException(nameof(table));

			if (table.IsEmpty) return;

			for (uint i = (uint)(table.Rows - 1); i > 1; i--) {
				uint k = NextUInt32(generator, i + 1);
				var tmp = table[k];
				table[k] = table[i];
				table[i] = tmp;
			}
		}
	}
}
