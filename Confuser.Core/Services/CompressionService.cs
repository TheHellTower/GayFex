using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Confuser.Helpers;
using dnlib.DotNet;
using K4os.Compression.LZ4;
using Microsoft.Extensions.DependencyInjection;
using SevenZip;
using SevenZip.Compression.LZMA;

namespace Confuser.Core.Services {
    internal class CompressionService : ICompressionService {
        private static readonly ImmutableDictionary<CompressionAlgorithm, object> Decompressors = BuildDecompressorAnnotationKeys();

        private InjectHelper InjectHelper { get; }
        private readonly IServiceProvider _serviceProvider;

        internal CompressionService(IServiceProvider provider) {
            _serviceProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            InjectHelper = new InjectHelper(provider);
        }

        private static ImmutableDictionary<CompressionAlgorithm, object> BuildDecompressorAnnotationKeys() {
            var builder = ImmutableDictionary.CreateBuilder<CompressionAlgorithm, object>();
            foreach (var algorithm in Enum.GetValues(typeof(CompressionAlgorithm)).OfType<CompressionAlgorithm>())
                builder.Add(algorithm, new object());
            return builder.ToImmutable();
        }

        /// <inheritdoc />
        public MethodDef GetRuntimeDecompressor(IConfuserContext context, ModuleDef module, CompressionAlgorithm algorithm, Action<IDnlibDef> init) {
            var injectResult = context.Annotations.GetOrCreate(module, Decompressors[algorithm], m => {
                var rt = _serviceProvider.GetRequiredService<CoreRuntimeService>().GetRuntimeModule();

                var decompressMethod = rt.GetRuntimeType(GetRuntimeTypeName(algorithm), module)
                    .Methods
                    .SingleOrDefault(method => method.Name == "Decompress");
                if (decompressMethod == null)
                    throw new NotSupportedException("Requested compression method is not supported for the target framework.");
                return InjectHelper.Inject(decompressMethod, module,
                    InjectBehaviors.RenameAndNestBehavior(context, module.GlobalType));
            });
            init(injectResult.Requested.Mapped);
            foreach (var injectedDependency in injectResult.InjectedDependencies) {
                init(injectedDependency.Mapped);
            }

            return injectResult.Requested.Mapped;
        }

        private static string GetRuntimeTypeName(CompressionAlgorithm algorithm) {
            switch (algorithm) {
                case CompressionAlgorithm.None:
                    return "Confuser.Core.Runtime.Compression.None";
                case CompressionAlgorithm.Deflate:
                    return "Confuser.Core.Runtime.Compression.Deflate";
                case CompressionAlgorithm.Lzma:
                    return "Confuser.Core.Runtime.Compression.Lzma";
                case CompressionAlgorithm.Lz4:
                    return "Confuser.Core.Runtime.Compression.Lz4";
                default:
                    throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unexpected value for compression algorithm.");
            }
        }

        /// <inheritdoc />
        public byte[] Compress(CompressionAlgorithm algorithm, ReadOnlySpan<byte> data, Action<double> progressFunc = null) {
            switch (algorithm) {
                case CompressionAlgorithm.None:
                    return data.ToArray();
                case CompressionAlgorithm.Deflate:
                    return CompressDeflate(data, progressFunc);
                case CompressionAlgorithm.Lzma:
                    return CompressLzma(data, progressFunc);
                case CompressionAlgorithm.Lz4:
                    return CompressLz4(data, progressFunc);
                default:
                    throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unexpected value for compression algorithm.");
            }
        }

        private static byte[] CompressDeflate(ReadOnlySpan<byte> data, Action<double> progressFunc) {
            const int headerSize = sizeof(int);

            using (var target = new MemoryStream()) {
                long fileSize = data.Length;
                for (var i = 0; i < headerSize; i++)
                    target.WriteByte((byte)(fileSize >> (8 * i)));

                using (var deflateStream = new DeflateStream(target, CompressionLevel.Optimal)) 
                    deflateStream.Write(data.ToArray(), 0, data.Length);

                return target.ToArray();
            }
        }

        private byte[] CompressLzma(ReadOnlySpan<byte> data, Action<double> progressFunc) {

            CoderPropID[] propIDs = {
                CoderPropID.DictionarySize,
                CoderPropID.PosStateBits,
                CoderPropID.LitContextBits,
                CoderPropID.LitPosBits,
                CoderPropID.Algorithm,
                CoderPropID.NumFastBytes,
                CoderPropID.MatchFinder,
                CoderPropID.EndMarker
            };
            object[] properties = {
                1 << 23,
                2,
                3,
                0,
                2,
                128,
                "bt4",
                false
            };

            using (var x = new MemoryStream()) {
                var encoder = new Encoder();
                encoder.SetCoderProperties(propIDs, properties);
                encoder.WriteCoderProperties(x);

                var length = BitConverter.GetBytes(data.Length);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(length);
            
                // Store 4 byte length value (little-endian)
                x.Write(length, 0, sizeof(int));

                ICodeProgress progress = null;
                if (progressFunc != null)
                    progress = new CompressionLogger(progressFunc, data.Length);

                var bufferArray = data.ToArray();
                encoder.Code(new MemoryStream(bufferArray), x, -1, -1, progress);

                return x.ToArray();
            }
        }

        private static byte[] CompressLz4(ReadOnlySpan<byte> data, Action<double> progressFunc) {
            const int headerSize = sizeof(int) * 2;

            var size = LZ4Codec.MaximumOutputSize(data.Length) + headerSize;
            while (true) {
                var target = new byte[size];
                var fileSize = data.Length;
                for (var i = 0; i < 4; i++)
                    target[i] = (byte)(fileSize >> (8 * i));
                
                var realSize = LZ4Codec.Encode(data, target.AsSpan(headerSize), LZ4Level.L12_MAX);
                
                for (var i = 4; i < 8; i++)
                    target[i] = (byte)(realSize >> (8 * i));

                if (realSize + headerSize == size) {
                    progressFunc?.Invoke(1.0);
                    return target;
                }

                if (realSize + headerSize > size) {
                    size = realSize;
                    continue;
                }

                var resizedResult = new byte[realSize + headerSize];
                Array.Copy(target, resizedResult, resizedResult.Length);
                progressFunc?.Invoke(1.0);
                return resizedResult;
            }
        }

        private sealed class CompressionLogger : ICodeProgress {
            private readonly Action<double> _progressFunc;
            private readonly int _size;

            public CompressionLogger(Action<double> progressFunc, int size) {
                _progressFunc = progressFunc;
                _size = size;
            }

            public void SetProgress(long inSize, long outSize) {
                var percentage = (double)inSize / _size;
                _progressFunc(percentage);
            }
        }
    }
}
