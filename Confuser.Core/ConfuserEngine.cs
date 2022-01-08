using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confuser.Core.Project;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using dnlib.PE;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using CopyrightAttribute = System.Reflection.AssemblyCopyrightAttribute;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using InformationalAttribute = System.Reflection.AssemblyInformationalVersionAttribute;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using ProductAttribute = System.Reflection.AssemblyProductAttribute;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace Confuser.Core {
	/// <summary>
	///     The processing engine of ConfuserEx.
	/// </summary>
	public static class ConfuserEngine {
		/// <summary>
		///     The version of ConfuserEx.
		/// </summary>
		public static readonly string Version;

		private static readonly string Copyright;

		static ConfuserEngine() {
			Assembly assembly = typeof(ConfuserEngine).Assembly;
			var nameAttr = (ProductAttribute)assembly.GetCustomAttributes(typeof(ProductAttribute), false)[0];
			var verAttr =
				(InformationalAttribute)assembly.GetCustomAttributes(typeof(InformationalAttribute), false)[0];
			var cpAttr = (CopyrightAttribute)assembly.GetCustomAttributes(typeof(CopyrightAttribute), false)[0];
			Version = string.Format("{0} {1}", nameAttr.Product, verAttr.InformationalVersion);
			Copyright = cpAttr.Copyright;

			AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
				try {
					var asmName = new AssemblyName(e.Name);
					foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
						if (asm.GetName().Name == asmName.Name)
							return asm;
					return null;
				}
				catch {
					return null;
				}
			};
		}

		/// <summary>
		///     Runs the engine with the specified parameters.
		/// </summary>
		/// <param name="parameters">The parameters.</param>
		/// <param name="token">The token used for cancellation.</param>
		/// <returns>Task to run the engine.</returns>
		/// <exception cref="System.ArgumentNullException">
		///     <paramref name="parameters" />.Project is <c>null</c>.
		/// </exception>
		public static Task<bool> Run(ConfuserParameters parameters, CancellationToken? token = null) {
			if (parameters.Project == null)
				throw new ArgumentNullException("parameters");
			if (token == null)
				token = new CancellationTokenSource().Token;
			return Task.Factory.StartNew(() => RunInternal(parameters, token.Value), token.Value);
		}

		/// <summary>
		///     Runs the engine.
		/// </summary>
		/// <param name="parameters">The parameters.</param>
		/// <param name="token">The cancellation token.</param>
		private static bool RunInternal(ConfuserParameters parameters, CancellationToken token) {
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddLogging(parameters.ConfigureLogging ?? delegate { });

			var tempServiceProvider = serviceCollection.BuildServiceProvider();
			var logger = tempServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("core");

			bool ok = false;
			try {
				Marker marker = parameters.GetMarker();

				// 2. Discover plugins
				logger.LogDebug("Discovering plugins...");

				var plugInContainer = parameters.GetPluginDiscovery().GetPlugins(parameters.Project, logger);
				var prots = plugInContainer.GetExports<IProtection, IProtectionMetadata>().ToArray();
				var packers = plugInContainer.GetExports<IPacker, IPackerMetadata>().ToArray();
				var components = plugInContainer.GetExports<IConfuserComponent>();

				logger.LogInformation("Discovered {0} protections, {1} packers.", prots.Count(), packers.Count());

				token.ThrowIfCancellationRequested();

				token.ThrowIfCancellationRequested();

				var sortedComponents = new List<IConfuserComponent>();
				sortedComponents.Add(new CoreComponent(parameters, marker));
				sortedComponents.AddRange(components.Select(l => l.Value));

				// 3. Resolve dependency
				logger.LogDebug("Resolving component dependency...");
				try {
					var resolver = new DependencyResolver(prots);
					sortedComponents.AddRange(resolver.SortDependency());
				}
				catch (CircularDependencyException ex) {
					logger.LogCritical(ex, "Plug-Ins have a circular dependency.");
					throw new ConfuserException(ex);
				}

				sortedComponents.AddRange(packers.Select(l => l.Value));

				token.ThrowIfCancellationRequested();

				// 4. Initialize components
				logger.LogDebug("Initializing...");
				foreach (var comp in sortedComponents) {
					try {
						comp.Initialize(serviceCollection);
					}
					catch (Exception ex) {
						logger.LogCritical(ex, "Error occurred during initialization of '{0}'.", comp.Name);
						throw new ConfuserException(ex);
					}

					token.ThrowIfCancellationRequested();
				}

				// 1. Setup context
				using (var context = new ConfuserContext(serviceCollection.BuildServiceProvider())) {
					context.Project = parameters.Project.Clone();
					context.PackerInitiated = parameters.PackerInitiated;

					PrintInfo(context, logger);

					var frameworkDiscoveries = plugInContainer.GetExports<IFrameworkDiscovery>();
					var installedFrameworks = frameworkDiscoveries.Select(l => l.Value).SelectMany(d => d.GetInstalledFrameworks(context)).ToArray();

					logger.LogDebug("Found {0} installed frameworks.", installedFrameworks.Length);

					try {
						// Enable watermarking by default
						context.Project.Rules.Insert(0, new Rule {
							new SettingItem<IProtection>(WatermarkingProtection.Id)
						});

						var asmResolver = new ConfuserAssemblyResolver {EnableTypeDefCache = true};
						asmResolver.EnableTypeDefCache = true;
						asmResolver.DefaultModuleContext = new ModuleContext(asmResolver);
						context.InternalResolver = asmResolver;
						context.BaseDirectory = Path.Combine(Environment.CurrentDirectory,
							context.Project.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) +
							Path.DirectorySeparatorChar);
						context.OutputDirectory = Path.Combine(context.Project.BaseDirectory,
							context.Project.OutputDirectory.TrimEnd(Path.DirectorySeparatorChar) +
							Path.DirectorySeparatorChar);
						foreach (string probePath in context.Project.ProbePaths)
							asmResolver.PostSearchPaths.Insert(0, Path.Combine(context.BaseDirectory, probePath));

						token.ThrowIfCancellationRequested();

						// 5. Load modules
						logger.LogInformation("Loading input modules...");
						marker.Initalize(prots, packers);
						MarkerResult markings = marker.MarkProject(context.Project, context, token);
						context.Modules = new ModuleSorter(markings.Modules).Sort().ToImmutableArray();
						foreach (var module in context.Modules)
							module.EnableTypeDefFindCache = false;
						context.OutputModules =
							Enumerable.Repeat(Memory<byte>.Empty, context.Modules.Count).ToImmutableArray();
						context.OutputSymbols =
							Enumerable.Repeat(Memory<byte>.Empty, context.Modules.Count).ToImmutableArray();
						context.OutputPaths = Enumerable.Repeat<string>(null, context.Modules.Count).ToImmutableArray();
						context.Packer = markings.Packer;
						context.ExternalModules = markings.ExternalModules;

						token.ThrowIfCancellationRequested();

						// 6. Build pipeline
						logger.LogDebug("Building pipeline...");
						var pipeline = new ProtectionPipeline();
						context.Pipeline = pipeline;
						foreach (var comp in sortedComponents) {
							comp.PopulatePipeline(pipeline);
						}

						token.ThrowIfCancellationRequested();

						//7. Run pipeline
						RunPipeline(pipeline, context, token);

						if (!context.PackerInitiated)
							logger.LogInformation("Done.");

						ok = true;
					}
					catch (Exception) {
						PrintEnvironmentInfo(context, installedFrameworks, logger);
						throw;
					}
				}
			}
			catch (AssemblyResolveException ex) {
				logger.LogCritical(ex,
					"Failed to resolve an assembly, check if all dependencies are present in the correct version.");
			}
			catch (TypeResolveException ex) {
				logger.LogCritical(ex,
					"Failed to resolve a type, check if all dependencies are present in the correct version.");
			}
			catch (MemberRefResolveException ex) {
				logger.LogCritical(ex,
					"Failed to resolve a member, check if all dependencies are present in the correct version.");
			}
			catch (IOException ex) {
				logger.LogCritical(ex,
					"An IO error occurred, check if all input/output locations are readable/writable.");
			}
			catch (OperationCanceledException) {
				logger.LogInformation("Operation canceled.");
			}
			catch (ConfuserException) {
				// Exception is already handled/logged, so just ignore and report failure
			}
			catch (Exception ex) {
				logger.LogCritical(ex, "Unknown error occurred.");
			}

			return ok;
		}

		/// <summary>
		///     Runs the protection pipeline.
		/// </summary>
		/// <param name="pipeline">The protection pipeline.</param>
		/// <param name="context">The context.</param>
		private static void RunPipeline(ProtectionPipeline pipeline, ConfuserContext context, CancellationToken token) {
			Func<IList<IDnlibDef>> getAllDefs = () =>
				context.Modules.SelectMany(module => module.FindDefinitions()).ToList();
			Func<ModuleDef, IList<IDnlibDef>> getModuleDefs = module => module.FindDefinitions().ToList();

			context.CurrentModuleIndex = -1;

			pipeline.ExecuteStage(PipelineStage.Inspection, Inspection, () => getAllDefs(), context, token);

			var options = new ModuleWriterOptionsBase[context.Modules.Count];
			for (int i = 0; i < context.Modules.Count; i++) {
				context.CurrentModuleIndex = i;
				context.CurrentModuleWriterOptions = null;

				pipeline.ExecuteStage(PipelineStage.BeginModule, BeginModule,
					() => getModuleDefs(context.CurrentModule), context, token);
				pipeline.ExecuteStage(PipelineStage.ProcessModule, ProcessModule,
					() => getModuleDefs(context.CurrentModule), context, token);
				pipeline.ExecuteStage(PipelineStage.OptimizeMethods, OptimizeMethods,
					() => getModuleDefs(context.CurrentModule), context, token);
				pipeline.ExecuteStage(PipelineStage.EndModule, EndModule, () => getModuleDefs(context.CurrentModule),
					context, token);

				options[i] = context.CurrentModuleWriterOptions;
			}

			for (int i = 0; i < context.Modules.Count; i++) {
				context.CurrentModuleIndex = i;
				context.CurrentModuleWriterOptions = options[i];

				pipeline.ExecuteStage(PipelineStage.WriteModule, WriteModule,
					() => getModuleDefs(context.CurrentModule), context, token);

				context.OutputModules = context.OutputModules.SetItem(i, context.CurrentModuleOutput);
				context.OutputSymbols = context.OutputSymbols.SetItem(i, context.CurrentModuleSymbol);
				context.CurrentModuleWriterOptions = null;
				context.CurrentModuleOutput = null;
				context.CurrentModuleSymbol = null;
			}

			context.CurrentModuleIndex = -1;

			pipeline.ExecuteStage(PipelineStage.Debug, DebugSymbols, () => getAllDefs(), context, token);
			pipeline.ExecuteStage(PipelineStage.Pack, Pack, () => getAllDefs(), context, token);
			pipeline.ExecuteStage(PipelineStage.SaveModules, SaveModules, () => getAllDefs(), context, token);
		}

		private static void Inspection(ConfuserContext context, CancellationToken token) {
			Debug.Assert(context != null, $"{nameof(context)} != null");

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("core");
			logger.LogInformation("Resolving dependencies...");
			foreach (var dependency in context.Modules
				.SelectMany(module => module.GetAssemblyRefs()
					.Select(asmRef => Tuple.Create(asmRef, module)))) {
				token.ThrowIfCancellationRequested();

				try {
					context.Resolver.ResolveThrow(dependency.Item1, dependency.Item2);
				}
				catch (AssemblyResolveException ex) {
					logger.LogCritical(ex, "Failed to resolve dependency of '{0}'.", dependency.Item2.Name);
					throw new ConfuserException(ex);
				}
			}

			logger.LogDebug("Checking Strong Name...");
			foreach (var module in context.Modules) {
				CheckStrongName(context, module, logger);
			}

			var marker = context.Registry.GetService<IMarkerService>();

			logger.LogDebug("Creating global .cctors...");
			foreach (var module in context.Modules) {
				var modType = module.GlobalType;
				if (modType == null) {
					modType = new TypeDefUser("", "<Module>", null) {
						Attributes = TypeAttributes.AnsiClass
					};
					module.Types.Add(modType);
					marker.Mark(context, modType, null);
				}

				var cctor = modType.FindOrCreateStaticConstructor();
				if (!marker.IsMarked(context, cctor))
					marker.Mark(context, cctor, null);
			}
		}

		private static void CheckStrongName(IConfuserContext context, ModuleDef module, ILogger logger) {
			var snKey = context.Annotations.Get<StrongNameKey>(module, Marker.SNKey);
			var snPubKeyBytes = context.Annotations.Get<StrongNamePublicKey>(module, Marker.SNPubKey)?.CreatePublicKey();
			var snDelaySign = context.Annotations.Get<bool>(module, Marker.SNDelaySig);

			if (snPubKeyBytes == null && snKey != null)
				snPubKeyBytes = snKey.PublicKey;

			bool moduleIsSignedOrDelayedSigned = module.IsStrongNameSigned || !module.Assembly.PublicKey.IsNullOrEmpty;

			bool isKeyProvided = snKey != null || (snDelaySign && snPubKeyBytes != null);

			if (!isKeyProvided && moduleIsSignedOrDelayedSigned)
				logger.LogWarning("[{0}] SN Key or SN public Key is not provided for a signed module, the output may not be working.", module.Name);
			else if (isKeyProvided && !moduleIsSignedOrDelayedSigned)
				logger.LogWarning("[{0}] SN Key or SN public Key is provided for an unsigned module, the output may not be working.", module.Name);
			else if (snPubKeyBytes != null && moduleIsSignedOrDelayedSigned &&
			         !module.Assembly.PublicKey.Data.SequenceEqual(snPubKeyBytes))
				logger.LogWarning("[{0}] Provided SN public Key and signed module's public key do not match, the output may not be working.",
					module.Name);
		}

		private static void CopyPEHeaders(PEHeadersOptions writerOptions, ModuleDefMD module) {
			var image = module.Metadata.PEImage;
			writerOptions.MajorImageVersion = image.ImageNTHeaders.OptionalHeader.MajorImageVersion;
			writerOptions.MajorLinkerVersion = image.ImageNTHeaders.OptionalHeader.MajorLinkerVersion;
			writerOptions.MajorOperatingSystemVersion = image.ImageNTHeaders.OptionalHeader.MajorOperatingSystemVersion;
			writerOptions.MajorSubsystemVersion = image.ImageNTHeaders.OptionalHeader.MajorSubsystemVersion;
			writerOptions.MinorImageVersion = image.ImageNTHeaders.OptionalHeader.MinorImageVersion;
			writerOptions.MinorLinkerVersion = image.ImageNTHeaders.OptionalHeader.MinorLinkerVersion;
			writerOptions.MinorOperatingSystemVersion = image.ImageNTHeaders.OptionalHeader.MinorOperatingSystemVersion;
			writerOptions.MinorSubsystemVersion = image.ImageNTHeaders.OptionalHeader.MinorSubsystemVersion;
		}

		private static void BeginModule(ConfuserContext context, CancellationToken token) {
			Debug.Assert(context != null, $"{nameof(context)} != null");

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("core");
			logger.LogInformation("Processing module '{0}'...", context.CurrentModule.Name);

			context.CurrentModuleWriterOptions = new ModuleWriterOptions(context.CurrentModule);
			CopyPEHeaders(context.CurrentModuleWriterOptions.PEHeadersOptions, context.CurrentModule);

			if (!context.CurrentModule.IsILOnly || context.CurrentModule.VTableFixups != null)
				context.RequestNative(true);

			var snKey = context.Annotations.Get<StrongNameKey>(context.CurrentModule, Marker.SNKey);
			var snPubKey = context.Annotations.Get<StrongNamePublicKey>(context.CurrentModule, Marker.SNPubKey);
			var snSigKey = context.Annotations.Get<StrongNameKey>(context.CurrentModule, Marker.SNSigKey);
			var snSigPubKey = context.Annotations.Get<StrongNamePublicKey>(context.CurrentModule, Marker.SNSigPubKey);

			var snDelaySig = context.Annotations.Get<bool>(context.CurrentModule, Marker.SNDelaySig, false);

			context.CurrentModuleWriterOptions.DelaySign = snDelaySig;

			if (snKey != null && snPubKey != null && snSigKey != null && snSigPubKey != null)
				context.CurrentModuleWriterOptions.InitializeEnhancedStrongNameSigning(context.CurrentModule, snSigKey, snSigPubKey, snKey, snPubKey);
			else if (snSigPubKey != null && snSigKey != null)
				context.CurrentModuleWriterOptions.InitializeEnhancedStrongNameSigning(context.CurrentModule, snSigKey, snSigPubKey);
			else
				context.CurrentModuleWriterOptions.InitializeStrongNameSigning(context.CurrentModule, snKey);

			if (snDelaySig) {
				context.CurrentModuleWriterOptions.StrongNamePublicKey = snPubKey;
				context.CurrentModuleWriterOptions.StrongNameKey = null;
			}

			foreach (var type in context.CurrentModule.GetTypes())
				foreach (var method in type.Methods) {
					token.ThrowIfCancellationRequested();

					if (method.Body != null) {
						method.Body.Instructions.SimplifyMacros(method.Body.Variables, method.Parameters);
					}
				}
		}

		private static void ProcessModule(ConfuserContext context, CancellationToken token) => 
			context.CurrentModuleWriterOptions.WriterEvent += (sender, e) => token.ThrowIfCancellationRequested();

		private static void OptimizeMethods(ConfuserContext context, CancellationToken token) {
			Debug.Assert(context != null, $"{nameof(context)} != null");

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("core");

			foreach (var type in context.CurrentModule.GetTypes())
				foreach (var method in type.Methods) {
					token.ThrowIfCancellationRequested();

					if (method.Body != null) {
						logger.LogTrace("Optimizing method '{0}'", method);
						method.Body.Instructions.OptimizeMacros();
					}
				}
		}

		private static void EndModule(ConfuserContext context, CancellationToken token) {
			string output = context.Modules[context.CurrentModuleIndex].Location;

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("core");

			if (output is not null) {
				if (!Path.IsPathRooted(output))
					output = Path.Combine(context.BaseDirectory, output);
				string relativeOutput = Utils.GetRelativePath(output, context.BaseDirectory);
				if (relativeOutput is null) {
					logger.LogWarning("Input file is not inside the base directory. Relative path can't be created. Placing file into output root." +
						Environment.NewLine + "Responsible file is: {0}", output);
					output = Path.GetFileName(output);
				} else {
					output = relativeOutput;
				}
			}
			else {
				output = context.CurrentModule.Name;
			}

			context.OutputPaths = context.OutputPaths.SetItem(context.CurrentModuleIndex, output);
		}

		private static void WriteModule(ConfuserContext context, CancellationToken token) {
			Debug.Assert(context != null, $"{nameof(context)} != null");

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("core");

			logger.LogInformation("Writing module '{0}'...", context.CurrentModule.Name);

			MemoryStream pdb = null, output = new MemoryStream();

			if (context.CurrentModule.PdbState != null) {
				pdb = new MemoryStream();
				context.CurrentModuleWriterOptions.WritePdb = true;
				context.CurrentModuleWriterOptions.PdbFileName =
					Path.ChangeExtension(Path.GetFileName(context.OutputPaths[context.CurrentModuleIndex]), "pdb");
				context.CurrentModuleWriterOptions.PdbStream = pdb;
			}

			token.ThrowIfCancellationRequested();

			if (context.CurrentModuleWriterOptions is ModuleWriterOptions)
				context.CurrentModule.Write(output, (ModuleWriterOptions)context.CurrentModuleWriterOptions);
			else
				context.CurrentModule.NativeWrite(output,
					(NativeModuleWriterOptions)context.CurrentModuleWriterOptions);

			token.ThrowIfCancellationRequested();

			context.CurrentModuleOutput = output.ToArray();
			if (context.CurrentModule.PdbState != null)
				context.CurrentModuleSymbol = pdb.ToArray();
		}

		private static void DebugSymbols(ConfuserContext context, CancellationToken token) {
			Debug.Assert(context != null, $"{nameof(context)} != null");

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("core");

			logger.LogInformation("Finalizing...");
			for (int i = 0; i < context.OutputModules.Count; i++) {
				token.ThrowIfCancellationRequested();

				if (context.OutputSymbols[i].IsEmpty)
					continue;
				string path = Path.GetFullPath(Path.Combine(context.OutputDirectory, context.OutputPaths[i]));
				string dir = Path.GetDirectoryName(path);
				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir);

				File.WriteAllBytes(Path.ChangeExtension(path, "pdb"), context.OutputSymbols[i].ToArray());
			}
		}

		private static void Pack(ConfuserContext context, CancellationToken token) {
			Debug.Assert(context != null, $"{nameof(context)} != null");

			if (context.Packer != null) {
				var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("core");
				logger.LogInformation("Packing...");
				context.Packer.Pack(context,
					new ProtectionParameters(context.Packer, context.Modules.OfType<IDnlibDef>().ToImmutableArray()),
					token);
			}
		}

		private static void SaveModules(ConfuserContext context, CancellationToken token) {
			Debug.Assert(context != null, $"{nameof(context)} != null");

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("core");

			context.InternalResolver.Clear();

			for (int i = 0; i < context.OutputModules.Count; i++) {
				token.ThrowIfCancellationRequested();

				var path = Path.GetFullPath(Path.Combine(context.OutputDirectory, context.OutputPaths[i]));

				var sourceModule = context.Modules[i];
				if (sourceModule.Metadata != null) {
					var sourcePath = Path.GetFullPath(sourceModule.Metadata.PEImage.Filename);

					if (string.Equals(path, sourcePath, StringComparison.OrdinalIgnoreCase)) {
						// we are doing an in place obfuscation. We need to make sure that the handle to the file is closed
						// in case a memory mapped file is in use to read the image.
						(sourceModule.Metadata.PEImage as IInternalPEImage)?.UnsafeDisableMemoryMappedIO();
					}
				}

				string dir = Path.GetDirectoryName(path);
				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir);
				logger.LogDebug("Saving to '{0}'...", path);
				File.WriteAllBytes(path, context.OutputModules[i].ToArray());
			}
		}

		/// <summary>
		///     Prints the copyright stuff and environment information.
		/// </summary>
		/// <param name="context">The working context.</param>
		private static void PrintInfo(ConfuserContext context, ILogger logger) {
			if (context.PackerInitiated) {
				logger.LogInformation("Protecting packer stub...");
			}
			else {
				logger.LogInformation("{0} {1}", Version, Copyright);

				var mono = Type.GetType("Mono.Runtime");
				logger.LogInformation(
					"Running on {0}, {1}, {2} bits",
					Environment.OSVersion,
					mono == null
						? ".NET Framework v" + Environment.Version
						: mono.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static)
							.Invoke(null, null),
					IntPtr.Size * 8);
			}
		}

		/// <summary>
		///     Prints the environment information when error occurred.
		/// </summary>
		/// <param name="context">The working context.</param>
		private static void PrintEnvironmentInfo(ConfuserContext context, IEnumerable<IInstalledFramework> installedFrameworks, ILogger logger) {
			try {
				if (context.PackerInitiated)
					return;

				var buildMsg = new StringBuilder();

				buildMsg.AppendLine("---BEGIN DEBUG INFO---");
				var firstFramework = true;
				foreach (var ver in installedFrameworks) {
					if (firstFramework) {
						buildMsg.AppendLine("Installed Framework Versions:");
						firstFramework = false;
					}

					buildMsg.AppendFormat("    {0}", ver.ToString()).AppendLine();
				}

				if (!firstFramework) buildMsg.AppendLine();

				if (context.Resolver != null) {
					buildMsg.AppendLine("Cached assemblies:");
					foreach (var asm in context.InternalResolver.GetCachedAssemblies().Where(def => def != null)) {
						if (string.IsNullOrEmpty(asm.ManifestModule.Location))
							buildMsg.AppendFormat("    {0}", asm.FullName).AppendLine();
						else
							buildMsg.AppendFormat("    {0} ({1})", asm.FullName, asm.ManifestModule.Location)
								.AppendLine();
						foreach (var reference in asm.Modules.OfType<ModuleDefMD>()
							.SelectMany(m => m.GetAssemblyRefs()))
							buildMsg.AppendFormat("        {0}", reference.FullName).AppendLine();
					}
				}

				buildMsg.AppendLine("---END DEBUG INFO---");
				logger.LogDebug(buildMsg.ToString());
			}
			catch {
				// Ignored
			}
		}
	}
}
