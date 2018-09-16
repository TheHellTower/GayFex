using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Confuser.Core.Helpers;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;
using SevenZip;
using SevenZip.Compression.LZMA;

namespace Confuser.Core.Services {
	internal class CompressionService : ICompressionService {
		static readonly object Decompressor = new object();

		private readonly IServiceProvider serviceProvider;

		internal CompressionService(IServiceProvider provider) =>
			serviceProvider = provider ?? throw new ArgumentNullException(nameof(provider));


		/// <inheritdoc />
		public MethodDef TryGetRuntimeDecompressor(IConfuserContext context, ModuleDef module, Action<IDnlibDef> init) {
			var decompressor = context.Annotations.Get<Tuple<MethodDef, List<IDnlibDef>>>(module, Decompressor);
			if (decompressor == null)
				return null;

			foreach (IDnlibDef member in decompressor.Item2)
				init(member);
			return decompressor.Item1;
		}

		/// <inheritdoc />
		public MethodDef GetRuntimeDecompressor(IConfuserContext context, ModuleDef module, Action<IDnlibDef> init) {
			var injectResult = context.Annotations.GetOrCreate(module, Decompressor, m => {
				var rt = serviceProvider.GetRequiredService<IRuntimeService>();
				var marker = context.Registry.GetRequiredService<IMarkerService>();

				var decompressMethod = rt.GetRuntimeType("Confuser.Runtime.Lzma").Methods.Where(method => method.Name == "Decompress").Single();
				return Confuser.Helpers.InjectHelper.Inject(decompressMethod, module, Confuser.Helpers.InjectBehaviors.RenameAndNestBehavior(context, module.GlobalType));
			});
			init(injectResult.Requested.Mapped);
			foreach (var injectedDependency in injectResult.InjectedDependencies) {
				init(injectedDependency.Mapped);
			}
			return injectResult.Requested.Mapped;
		}

		/// <inheritdoc />
		public byte[] Compress(byte[] data, Action<double> progressFunc = null) {
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

			var x = new MemoryStream();
			var encoder = new Encoder();
			encoder.SetCoderProperties(propIDs, properties);
			encoder.WriteCoderProperties(x);
			Int64 fileSize;
			fileSize = data.Length;
			for (int i = 0; i < 8; i++)
				x.WriteByte((Byte)(fileSize >> (8 * i)));

			ICodeProgress progress = null;
			if (progressFunc != null)
				progress = new CompressionLogger(progressFunc, data.Length);
			encoder.Code(new MemoryStream(data), x, -1, -1, progress);

			return x.ToArray();
		}

		class CompressionLogger : ICodeProgress {
			readonly Action<double> progressFunc;
			readonly int size;

			public CompressionLogger(Action<double> progressFunc, int size) {
				this.progressFunc = progressFunc;
				this.size = size;
			}

			public void SetProgress(long inSize, long outSize) {
				double precentage = (double)inSize / size;
				progressFunc(precentage);
			}
		}
	}
}
