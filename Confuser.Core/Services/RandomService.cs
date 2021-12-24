using System;
using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Confuser.Core.Services {
	// TODO: There is more performance to be found here, once the hash algorithmns are properly able to handle Span.
	/// <summary>
	///     Implementation of <see cref="IRandomService" />.
	/// </summary>
	internal sealed class RandomService : IRandomService {
		private readonly ReadOnlyMemory<byte> seed; //32 bytes

		public string SeedString { get; }

		/// <summary>
		///     Initializes a new instance of the <see cref="RandomService" /> class.
		/// </summary>
		/// <param name="seed">The project seed.</param>
		public RandomService(string seed)
		{
			SeedString = string.IsNullOrEmpty(seed) ? Guid.NewGuid().ToString() : seed;
			this.seed = RandomGenerator.Seed(GetHashAlgorithm(), SeedString);
		}

		/// <inheritdoc />
		public IRandomGenerator GetRandomGenerator(string id) {
			if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

			var hashAlgo = GetHashAlgorithm();
			byte[] newSeed = seed.ToArray();
			byte[] idHash = hashAlgo.ComputeHash(Encoding.UTF8.GetBytes(id));
			Debug.Assert(newSeed.Length == idHash.Length, $"{nameof(newSeed)}.Length == {nameof(idHash)}.Length");
			for (int i = 0; i < newSeed.Length; i++)
				newSeed[i] ^= idHash[i];
			return new RandomGenerator(hashAlgo, hashAlgo.ComputeHash(newSeed));
		}

		private static HashAlgorithm GetHashAlgorithm() {
			if (CryptoConfig.AllowOnlyFipsAlgorithms)
				return HashAlgorithm.Create();
			else
				return SHA256.Create();
		}

		/// <summary>
		///     The default random value generator.
		/// </summary>
		private sealed class RandomGenerator : IRandomGenerator, IDisposable {
			/// <summary>
			///     The prime numbers used for generation
			/// </summary>
			private static readonly byte[] primes = {7, 11, 23, 37, 43, 59, 71};

			private readonly HashAlgorithm hashAlgo;
			private int mixIndex;
			private readonly Memory<byte> fullState;
			private Memory<byte> state;

			/// <summary>
			///     Initializes a new instance of the <see cref="RandomGenerator" /> class.
			/// </summary>
			/// <param name="seed">The seed.</param>
			internal RandomGenerator(HashAlgorithm hashAlgo, Span<byte> seed) {
				Debug.Assert(hashAlgo != null, $"{nameof(hashAlgo)} != null");
				this.hashAlgo = hashAlgo;

				Debug.Assert(seed.Length == hashAlgo.HashSize / 8,
					$"{nameof(seed)}.Length == {nameof(hashAlgo)}.HashSize / 8 ({hashAlgo.HashSize / 8})");

				fullState = new byte[seed.Length];
				seed.CopyTo(fullState.Span);
				state = fullState;
				mixIndex = 0;
			}

			/// <summary>
			///     Creates a seed buffer.
			/// </summary>
			/// <param name="hashAlgo">The algorithm implementation to create hashes.</param>
			/// <param name="seed">The seed data.</param>
			/// <returns>The seed buffer.</returns>
			internal static Memory<byte> Seed(HashAlgorithm hashAlgo, string seed) {
				Debug.Assert(hashAlgo != null, $"{nameof(hashAlgo)} != null");

				byte[] ret;
				if (!string.IsNullOrEmpty(seed))
					ret = hashAlgo.ComputeHash(Encoding.UTF8.GetBytes(seed));
				else
					ret = hashAlgo.ComputeHash(Guid.NewGuid().ToByteArray());

				for (int i = 0; i < ret.Length; i++) {
					ret[i] *= primes[i % primes.Length];
					ret = hashAlgo.ComputeHash(ret);
				}

				return ret;
			}

			/// <summary>
			///     Refills the state buffer.
			/// </summary>
			void NextState() {
				var stateSpan = fullState.Span;
				for (int i = 0; i < stateSpan.Length; i++)
					stateSpan[i] ^= primes[mixIndex = (mixIndex + 1) % primes.Length];
				var tmpInputArray = ArrayPool<byte>.Shared.Rent(stateSpan.Length);
				try {
					stateSpan.CopyTo(tmpInputArray);
					var hash = hashAlgo.ComputeHash(tmpInputArray);
					hash.CopyTo(fullState);
					state = fullState;
				}
				finally {
					ArrayPool<byte>.Shared.Return(tmpInputArray);
				}
			}

			/// <summary>
			///     Fills the specified buffer with random bytes.
			/// </summary>
			/// <param name="buffer">The buffer.</param>
			/// <exception cref="ArgumentNullException"><paramref name="buffer" /> is <see langword="null"/>.</exception>
			public void NextBytes(Span<byte> buffer) {
				if (buffer == null) throw new ArgumentNullException(nameof(buffer));

				while (buffer.Length > 0) {
					if (buffer.Length >= state.Length) {
						state.Span.CopyTo(buffer);
						buffer = buffer.Slice(state.Length);
						state = Memory<byte>.Empty;
					}
					else {
						state.Span.Slice(0, buffer.Length).CopyTo(buffer);
						state = state.Slice(buffer.Length);
						buffer = Span<byte>.Empty;
					}

					if (state.IsEmpty)
						NextState();
				}
			}

			/// <summary>
			///     Returns a random byte.
			/// </summary>
			/// <returns>Requested random byte.</returns>
			public byte NextByte() {
				byte ret = state.Span[0];
				state = state.Slice(1);
				if (state.IsEmpty)
					NextState();
				return ret;
			}

			/// <summary>
			///     Returns a random boolean value.
			/// </summary>
			/// <returns>Requested random boolean value.</returns>
			public bool NextBoolean() {
				byte s = NextByte();
				return s % 2 == 0;
			}

			#region IDisposable Support

			private bool _disposed = false;

			void Dispose(bool disposing) {
				if (!_disposed) {
					if (disposing) {
						hashAlgo.Dispose();
					}

					_disposed = true;
				}
			}

			public void Dispose() {
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			#endregion
		}
	}
}
