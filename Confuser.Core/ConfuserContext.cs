using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Confuser.Core.Project;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Core {
	/// <summary>
	///     Context providing information on the current protection process.
	/// </summary>
	public class ConfuserContext : IConfuserContext {
		readonly Annotations annotations = new Annotations();

		/// <summary>
		///     Gets the project being processed.
		/// </summary>
		/// <value>The project.</value>
		public ConfuserProject Project { get; internal set; }

		internal bool PackerInitiated { get; set; }

		/// <summary>
		///     Gets the annotation storage.
		/// </summary>
		/// <value>The annotation storage.</value>
		public Annotations Annotations {
			get { return annotations; }
		}

		IAnnotations IConfuserContext.Annotations => Annotations;

		/// <summary>
		///     Gets the service registry.
		/// </summary>
		/// <value>The service registry.</value>
		public IServiceProvider Registry { get; }

		/// <summary>
		///     Gets the assembly resolver.
		/// </summary>
		/// <value>The assembly resolver.</value>
		public AssemblyResolver Resolver { get; internal set; }

		IAssemblyResolver IConfuserContext.Resolver => Resolver;

		/// <summary>
		///     Gets the modules being protected.
		/// </summary>
		/// <value>The modules being protected.</value>
		public IImmutableList<ModuleDefMD> Modules { get; internal set; }

		/// <summary>
		///     Gets the external modules.
		/// </summary>
		/// <value>The external modules.</value>
		public IImmutableList<byte[]> ExternalModules { get; internal set; }

		/// <summary>
		///     Gets the base directory.
		/// </summary>
		/// <value>The base directory.</value>
		public string BaseDirectory { get; internal set; }

		/// <summary>
		///     Gets the output directory.
		/// </summary>
		/// <value>The output directory.</value>
		public string OutputDirectory { get; internal set; }

		/// <summary>
		///     Gets the packer.
		/// </summary>
		/// <value>The packer.</value>
		public IPacker Packer { get; internal set; }

		/// <summary>
		///     Gets the current processing pipeline.
		/// </summary>
		/// <value>The processing pipeline.</value>
		public ProtectionPipeline Pipeline { get; internal set; }

		IProtectionPipeline IConfuserContext.Pipeline => Pipeline;

		/// <summary>
		///     Gets the <c>byte[]</c> of modules after protected, or null if module is not protected yet.
		/// </summary>
		/// <value>The list of <c>byte[]</c> of protected modules.</value>
		public IImmutableList<byte[]> OutputModules { get; internal set; }

		/// <summary>
		///     Gets the <c>byte[]</c> of module debug symbols after protected, or null if module is not protected yet.
		/// </summary>
		/// <value>The list of <c>byte[]</c> of module debug symbols.</value>
		public IImmutableList<byte[]> OutputSymbols { get; internal set; }

		/// <summary>
		///     Gets the relative output paths of module, or null if module is not protected yet.
		/// </summary>
		/// <value>The relative output paths of protected modules.</value>
		public IImmutableList<string> OutputPaths { get; internal set; }

		/// <summary>
		///     Gets the current module index.
		/// </summary>
		/// <value>The current module index.</value>
		public int CurrentModuleIndex { get; internal set; }

		/// <summary>
		///     Gets the current module.
		/// </summary>
		/// <value>The current module.</value>
		public ModuleDefMD CurrentModule {
			get { return CurrentModuleIndex == -1 ? null : Modules[CurrentModuleIndex]; }
		}

		/// <summary>
		///     Gets the writer options of the current module.
		/// </summary>
		/// <value>The writer options.</value>
		public ModuleWriterOptionsBase CurrentModuleWriterOptions { get; internal set; }

		/// <summary>
		///     Gets output <c>byte[]</c> of the current module
		/// </summary>
		/// <value>The output <c>byte[]</c>.</value>
		public byte[] CurrentModuleOutput { get; internal set; }

		/// <summary>
		///     Gets output <c>byte[]</c> debug symbol of the current module
		/// </summary>
		/// <value>The output <c>byte[]</c> debug symbol.</value>
		public byte[] CurrentModuleSymbol { get; internal set; }

		internal ConfuserContext(IServiceProvider serviceProvider) {
			Registry = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		}

		/// <summary>
		///     Requests the current module to be written as mix-mode module, and return the native writer options.
		/// </summary>
		/// <returns>The native writer options.</returns>
		public NativeModuleWriterOptions RequestNative() {
			if (CurrentModule == null)
				return null;
			if (CurrentModuleWriterOptions == null)
				CurrentModuleWriterOptions = new NativeModuleWriterOptions(CurrentModule, true);

			if (CurrentModuleWriterOptions is NativeModuleWriterOptions)
				return (NativeModuleWriterOptions)CurrentModuleWriterOptions;
			var newOptions = new NativeModuleWriterOptions(CurrentModule, true);
			// Clone the current options to the new options
			newOptions.AddCheckSum = CurrentModuleWriterOptions.AddCheckSum;
			newOptions.Cor20HeaderOptions = CurrentModuleWriterOptions.Cor20HeaderOptions;
			newOptions.Logger = CurrentModuleWriterOptions.Logger;
			newOptions.MetadataLogger = CurrentModuleWriterOptions.MetadataLogger;
			newOptions.MetadataOptions = CurrentModuleWriterOptions.MetadataOptions;
			newOptions.ModuleKind = CurrentModuleWriterOptions.ModuleKind;
			newOptions.PEHeadersOptions = CurrentModuleWriterOptions.PEHeadersOptions;
			newOptions.ShareMethodBodies = CurrentModuleWriterOptions.ShareMethodBodies;
			newOptions.StrongNameKey = CurrentModuleWriterOptions.StrongNameKey;
			newOptions.StrongNamePublicKey = CurrentModuleWriterOptions.StrongNamePublicKey;
			newOptions.Win32Resources = CurrentModuleWriterOptions.Win32Resources;
			CurrentModuleWriterOptions = newOptions;
			return newOptions;
		}

		public IProtectionSettings GetParameters(IDnlibDef target) =>
			ProtectionParameters.GetParameters(this, target);
	}
}
