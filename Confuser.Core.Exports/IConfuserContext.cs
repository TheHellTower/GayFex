using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace Confuser.Core {
	/// <summary>
	///     Context providing information on the current protection process.
	/// </summary>
	public interface IConfuserContext {
		/// <summary>
		///     Gets the annotation storage.
		/// </summary>
		/// <value>The annotation storage.</value>
		IAnnotations Annotations { get; }

		/// <summary>
		///     Gets the service registry.
		/// </summary>
		/// <value>The service registry.</value>
		IServiceProvider Registry { get; }

		/// <summary>
		///     Gets the current processing pipeline.
		/// </summary>
		/// <value>The processing pipeline.</value>
		IProtectionPipeline Pipeline { get; }

		/// <summary>
		///     Gets the modules being protected.
		/// </summary>
		/// <value>The modules being protected.</value>
		IImmutableList<ModuleDefMD> Modules { get; }

		/// <summary>
		///     Gets the assembly resolver.
		/// </summary>
		/// <value>The assembly resolver.</value>
		IAssemblyResolver Resolver { get; }

		/// <summary>
		///     Gets the external modules.
		/// </summary>
		/// <value>The external modules.</value>
		IImmutableList<ReadOnlyMemory<byte>> ExternalModules { get; }

		/// <summary>
		///     Gets the packer.
		/// </summary>
		/// <value>The packer.</value>
		IPacker Packer { get; }

		/// <summary>
		///     Gets the output directory.
		/// </summary>
		/// <value>The output directory.</value>
		string OutputDirectory { get; }

		/// <summary>
		///     Gets the <c>byte[]</c> of modules after protected, or null if module is not protected yet.
		/// </summary>
		/// <value>The list of <c>byte[]</c> of protected modules.</value>
		IImmutableList<byte[]> OutputModules { get; }

		/// <summary>
		///     Gets the <c>byte[]</c> of module debug symbols after protected, or null if module is not protected yet.
		/// </summary>
		/// <value>The list of <c>byte[]</c> of module debug symbols.</value>
		IImmutableList<byte[]> OutputSymbols { get; }

		/// <summary>
		///     Gets the relative output paths of module, or null if module is not protected yet.
		/// </summary>
		/// <value>The relative output paths of protected modules.</value>
		IImmutableList<string> OutputPaths { get; }

		/// <summary>
		///     Gets the current module index.
		/// </summary>
		/// <value>The current module index.</value>
		int CurrentModuleIndex { get; }

		/// <summary>
		///     Gets the current module.
		/// </summary>
		/// <value>The current module.</value>
		ModuleDefMD CurrentModule { get; }

		/// <summary>
		///     Gets the writer options of the current module.
		/// </summary>
		/// <value>The writer options.</value>
		ModuleWriterOptionsBase CurrentModuleWriterOptions { get; }

		/// <summary>
		///     Gets the protection parameters of the specified target.
		/// </summary>
		/// <param name="target">The protection target.</param>
		/// <returns>The parameters</returns>
		IProtectionSettings GetParameters(IDnlibDef target);
	}
}
