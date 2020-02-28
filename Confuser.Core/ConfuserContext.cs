using System;
using System.Collections.Immutable;
using Confuser.Core.Project;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace Confuser.Core {
	/// <inheritdoc cref="IConfuserContext" />
	public sealed class ConfuserContext : IConfuserContext, IDisposable {
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
		public Annotations Annotations { get; } = new Annotations();

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

		/// <inheritdoc />
		public IImmutableList<ModuleDefMD> Modules { get; internal set; }

		/// <inheritdoc />
		public IImmutableList<ReadOnlyMemory<byte>> ExternalModules { get; internal set; }

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

		/// <inheritdoc />
		public IImmutableList<Memory<byte>> OutputModules { get; internal set; }

		/// <inheritdoc />
		public IImmutableList<Memory<byte>> OutputSymbols { get; internal set; }

		/// <inheritdoc />
		public IImmutableList<string> OutputPaths { get; internal set; }

		/// <inheritdoc />
		public int CurrentModuleIndex { get; internal set; }

		/// <inheritdoc />
		public ModuleDefMD CurrentModule => CurrentModuleIndex == -1 ? null : Modules[CurrentModuleIndex];

		/// <inheritdoc />
		public ModuleWriterOptionsBase CurrentModuleWriterOptions { get; internal set; }

		/// <summary>
		///     Gets output <c>byte[]</c> of the current module
		/// </summary>
		/// <value>The output <c>byte[]</c>.</value>
		public Memory<byte> CurrentModuleOutput { get; internal set; }

		/// <summary>
		///     Gets output <c>byte[]</c> debug symbol of the current module
		/// </summary>
		/// <value>The output <c>byte[]</c> debug symbol.</value>
		public Memory<byte> CurrentModuleSymbol { get; internal set; }

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

			// Clone the current options to the new options
			var newOptions = new NativeModuleWriterOptions(CurrentModule, true) {
				AddCheckSum = CurrentModuleWriterOptions.AddCheckSum,
				Cor20HeaderOptions = CurrentModuleWriterOptions.Cor20HeaderOptions,
				Logger = CurrentModuleWriterOptions.Logger,
				MetadataLogger = CurrentModuleWriterOptions.MetadataLogger,
				MetadataOptions = CurrentModuleWriterOptions.MetadataOptions,
				ModuleKind = CurrentModuleWriterOptions.ModuleKind,
				PEHeadersOptions = CurrentModuleWriterOptions.PEHeadersOptions,
				ShareMethodBodies = CurrentModuleWriterOptions.ShareMethodBodies,
				DelaySign = CurrentModuleWriterOptions.DelaySign,
				StrongNameKey = CurrentModuleWriterOptions.StrongNameKey,
				StrongNamePublicKey = CurrentModuleWriterOptions.StrongNamePublicKey,
				Win32Resources = CurrentModuleWriterOptions.Win32Resources
			};
			CurrentModuleWriterOptions = newOptions;
			return newOptions;
		}

		public IProtectionSettings GetParameters(IDnlibDef target) =>
			ProtectionParameters.GetParameters(this, target);

		private void Dispose(bool disposing) {
			if (!disposing) return;

			if (Modules != null)
				foreach (var moduleDef in Modules)
					moduleDef.Dispose();
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~ConfuserContext() => Dispose(false);
	}
}
