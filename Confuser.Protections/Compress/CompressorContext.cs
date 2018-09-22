using System;
using System.Collections.Generic;
using System.Diagnostics;
using Confuser.Core.Services;
using dnlib.DotNet;

namespace Confuser.Protections.Compress {
	internal sealed class CompressorContext {
		internal AssemblyDef Assembly;
		internal IKeyDeriver Deriver;
		internal ReadOnlyMemory<byte> EncryptedModule;
		internal MethodDef EntryPoint;
		internal uint EntryPointToken;
		internal Memory<byte> KeySig;
		internal uint KeyToken;
		internal ModuleKind Kind;
		internal List<(uint Offset, uint Flags, UTF8String Value)> ManifestResources;
		internal int ModuleIndex;
		internal string ModuleName;
		internal byte[] OriginModule;
		internal ModuleDef OriginModuleDef;
		internal bool CompatMode;
		internal Helpers.LateMutationFieldUpdate KeyTokenLoadUpdate;

		internal ReadOnlyMemory<byte> Encrypt(ICompressionService compress, ReadOnlyMemory<byte> source, uint seed, Action<double> progressFunc) {
			if (compress == null) throw new ArgumentNullException(nameof(compress));
			if (progressFunc == null) throw new ArgumentNullException(nameof(progressFunc));

			Span<byte> data = new byte[source.Length];
			source.Span.CopyTo(data);
			Span<uint> dst = stackalloc uint[0x10];
			Span<uint> src = stackalloc uint[0x10];
			ulong state = seed;
			for (int i = 0; i < 0x10; i++) {
				state = (state * state) % 0x143fc089;
				src[i] = (uint)state;
				dst[i] = (uint)((state * state) % 0x444d56fb);
			}
			Span<uint> key = stackalloc uint[0x10];
			Deriver.DeriveKey(dst, src, key);

			var z = (uint)(state % 0x8a5cb7);
			for (int i = 0; i < data.Length; i++) {
				data[i] ^= (byte)state;
				if ((i & 0xff) == 0)
					state = (state * state) % 0x8a5cb7;
			}
			Span<byte> compressedData = compress.Compress(data.ToArray(), progressFunc);
			Memory<byte> encryptedData = new byte[(compressedData.Length + 3) & ~3];

			int keyIndex = 0;
			for (int i = 0; i < encryptedData.Length; i += 4) {
				EncryptData(
					compressedData.Slice(start: i, length: Math.Min(4, compressedData.Length - i)),
					encryptedData.Span.Slice(start: i, length: 4),
					key, keyIndex);
				keyIndex++;
			}

			return encryptedData;
		}
		
		private static void EncryptData(ReadOnlySpan<byte> src, Span<byte> dst, Span<uint> key, int keyIndex) {
			Debug.Assert(src.Length > 0 && src.Length <= 4, $"{nameof(src)}.Length > 0 && {nameof(src)}.Length <= 4");
			Debug.Assert(dst.Length == 4, $"{nameof(dst)}.Length = 4");

			var datum = (uint)src[0];
			if (src.Length >= 2) datum |= (uint)(src[1] << 8);
			if (src.Length >= 3) datum |= (uint)(src[2] << 16);
			if (src.Length == 4) datum |= (uint)(src[3] << 24);

			uint encrypted = datum ^ key[keyIndex & 0xf];
			key[keyIndex & 0xf] = (key[keyIndex & 0xf] ^ datum) + 0x3ddb2819;
			dst[0] = (byte)(encrypted >> 0);
			dst[1] = (byte)(encrypted >> 8);
			dst[2] = (byte)(encrypted >> 16);
			dst[3] = (byte)(encrypted >> 24);
		}
	}
}
