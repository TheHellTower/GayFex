using System;
using dnlib.DotNet;

namespace Confuser.Core.Services {
	/// <summary>
	///     Provides methods to do compression and inject decompression algorithm.
	/// </summary>
	public interface ICompressionService {
		/// <summary>
		///     Gets the runtime decompression method in the module and inject if it does not exists.
		/// </summary>
		/// <param name="context">The working confuser context.</param>
		/// <param name="module">The module which the decompression method resides in.</param>
		/// <param name="init">The initializing method for injected helper definitions.</param>
		/// <returns>The requested decompression method with signature 'static Byte[] (Byte[])'.</returns>
		MethodDef GetRuntimeDecompressor(IConfuserContext context, ModuleDef module, Action<IDnlibDef> init);

		/// <summary>
		///     Compresses the specified data.
		/// </summary>
		/// <param name="data">The buffer storing the data.</param>
		/// <param name="progressFunc">The function that receive the progress of compression.</param>
		/// <returns>The compressed data.</returns>
		byte[] Compress(byte[] data, Action<double> progressFunc = null);
	}
}
