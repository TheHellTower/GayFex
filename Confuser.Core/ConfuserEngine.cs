using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Diagnostics;
using CopyrightAttribute = System.Reflection.AssemblyCopyrightAttribute;
using InformationalAttribute = System.Reflection.AssemblyInformationalVersionAttribute;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using ProductAttribute = System.Reflection.AssemblyProductAttribute;
using TypeAttributes = dnlib.DotNet.TypeAttributes;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using System.Text;

namespace Confuser.Core {
	/// <summary>
	///     The processing engine of ConfuserEx.
	/// </summary>
	public static class ConfuserEngine {
		/// <summary>
		///     The version of ConfuserEx.
		/// </summary>
		public static readonly string Version;

		static readonly string Copyright;

		static ConfuserEngine() {
			Assembly assembly = typeof(ConfuserEngine).Assembly;
			var nameAttr = (ProductAttribute)assembly.GetCustomAttributes(typeof(ProductAttribute), false)[0];
			var verAttr = (InformationalAttribute)assembly.GetCustomAttributes(typeof(InformationalAttribute), false)[0];
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

			var tempServiceProvder = serviceCollection.BuildServiceProvider();
			var logger = tempServiceProvder.GetRequiredService<ILoggerFactory>().CreateLogger("core");

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
				var context = new ConfuserContext(serviceCollection.BuildServiceProvider());
				context.Project = parameters.Project.Clone();
				context.PackerInitiated = parameters.PackerInitiated;

				PrintInfo(context, logger);

				try {
					var asmResolver = new AssemblyResolver();
					asmResolver.EnableTypeDefCache = true;
					asmResolver.DefaultModuleContext = new ModuleContext(asmResolver);
					context.Resolver = asmResolver;
					context.BaseDirectory = Path.Combine(Environment.CurrentDirectory, parameters.Project.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
					context.OutputDirectory = Path.Combine(parameters.Project.BaseDirectory, parameters.Project.OutputDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
					foreach (string probePath in parameters.Project.ProbePaths)
						asmResolver.PostSearchPaths.Insert(0, Path.Combine(context.BaseDirectory, probePath));

					token.ThrowIfCancellationRequested();


					// 5. Load modules
					logger.LogInformation("Loading input modules...");
					marker.Initalize(prots, packers);
					MarkerResult markings = marker.MarkProject(parameters.Project, context, token);
					context.Modules = new ModuleSorter(markings.Modules).Sort().ToImmutableArray();
					foreach (var module in context.Modules)
						module.EnableTypeDefFindCache = false;
					context.OutputModules = Enumerable.Repeat<byte[]>(null, context.Modules.Count).ToImmutableArray();
					context.OutputSymbols = Enumerable.Repeat<byte[]>(null, context.Modules.Count).ToImmutableArray();
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
					PrintEnvironmentInfo(context, logger);
					throw;
				}
			}
			catch (AssemblyResolveException ex) {
				logger.LogCritical(ex, "Failed to resolve an assembly, check if all dependencies are present in the correct version.");
			}
			catch (TypeResolveException ex) {
				logger.LogCritical(ex, "Failed to resolve a type, check if all dependencies are present in the correct version.");
			}
			catch (MemberRefResolveException ex) {
				logger.LogCritical(ex, "Failed to resolve a member, check if all dependencies are present in the correct version.");
			}
			catch (IOException ex) {
				logger.LogCritical(ex, "An IO error occurred, check if all input/output locations are readable/writable.");
			}
			catch (OperationCanceledException) {
				logger.LogInformation("Operation canceled.");
			}
			catch (ConfuserException) {
				// Exception is already handled/logged, so just ignore and report failure
			}
			catch (Exception ex) {
				logger.LogCritical("Unknown error occurred.", ex);
			}
			return ok;
		}

		/// <summary>
		///     Runs the protection pipeline.
		/// </summary>
		/// <param name="pipeline">The protection pipeline.</param>
		/// <param name="context">The context.</param>
		private static void RunPipeline(ProtectionPipeline pipeline, ConfuserContext context, CancellationToken token) {
			Func<IList<IDnlibDef>> getAllDefs = () => context.Modules.SelectMany(module => module.FindDefinitions()).ToList();
			Func<ModuleDef, IList<IDnlibDef>> getModuleDefs = module => module.FindDefinitions().ToList();

			context.CurrentModuleIndex = -1;

			pipeline.ExecuteStage(PipelineStage.Inspection, Inspection, () => getAllDefs(), context, token);

			var options = new ModuleWriterOptionsBase[context.Modules.Count];
			for (int i = 0; i < context.Modules.Count; i++) {
				context.CurrentModuleIndex = i;
				context.CurrentModuleWriterOptions = null;

				pipeline.ExecuteStage(PipelineStage.BeginModule, BeginModule, () => getModuleDefs(context.CurrentModule), context, token);
				pipeline.ExecuteStage(PipelineStage.ProcessModule, ProcessModule, () => getModuleDefs(context.CurrentModule), context, token);
				pipeline.ExecuteStage(PipelineStage.OptimizeMethods, OptimizeMethods, () => getModuleDefs(context.CurrentModule), context, token);
				pipeline.ExecuteStage(PipelineStage.EndModule, EndModule, () => getModuleDefs(context.CurrentModule), context, token);

				options[i] = context.CurrentModuleWriterOptions;
			}

			for (int i = 0; i < context.Modules.Count; i++) {
				context.CurrentModuleIndex = i;
				context.CurrentModuleWriterOptions = options[i];

				pipeline.ExecuteStage(PipelineStage.WriteModule, WriteModule, () => getModuleDefs(context.CurrentModule), context, token);

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
				var snKey = context.Annotations.Get<StrongNameKey>(module, Marker.SNKey);
				if (snKey == null && module.IsStrongNameSigned)
					logger.LogWarning("[{0}] SN Key is not provided for a signed module, the output may not be working.", module.Name);
				else if (snKey != null && !module.IsStrongNameSigned)
					logger.LogWarning("[{0}] SN Key is provided for an unsigned module, the output may not be working.", module.Name);
				else if (snKey != null && module.IsStrongNameSigned && !module.Assembly.PublicKey.Data.SequenceEqual(snKey.PublicKey))
					logger.LogWarning("[{0}] Provided SN Key and signed module's public key do not match, the output may not be working.", module.Name);
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

			logger.LogDebug("Watermarking...");
			foreach (var module in context.Modules) {
				var attrRef = module.CorLibTypes.GetTypeRef("System", "Attribute");
				var attrType = new TypeDefUser("", "ConfusedByAttribute", attrRef);
				module.Types.Add(attrType);
				marker.Mark(context, attrType, null);

				var ctor = new MethodDefUser(
					".ctor",
					MethodSig.CreateInstance(module.CorLibTypes.Void, module.CorLibTypes.String),
					MethodImplAttributes.Managed,
					MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
				ctor.Body = new CilBody();
				ctor.Body.MaxStack = 1;
				ctor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
				ctor.Body.Instructions.Add(OpCodes.Call.ToInstruction(new MemberRefUser(module, ".ctor", MethodSig.CreateInstance(module.CorLibTypes.Void), attrRef)));
				ctor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
				attrType.Methods.Add(ctor);
				marker.Mark(context, ctor, null);

				var attr = new CustomAttribute(ctor);
				attr.ConstructorArguments.Add(new CAArgument(module.CorLibTypes.String, Version));

				module.CustomAttributes.Add(attr);
			}
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
			context.CurrentModuleWriterOptions.WriterEvent += (sender, e) => token.ThrowIfCancellationRequested();
			CopyPEHeaders(context.CurrentModuleWriterOptions.PEHeadersOptions, context.CurrentModule);

			if (!context.CurrentModule.IsILOnly || context.CurrentModule.VTableFixups != null)
				context.RequestNative();

			var snKey = context.Annotations.Get<StrongNameKey>(context.CurrentModule, Marker.SNKey);
			context.CurrentModuleWriterOptions.InitializeStrongNameSigning(context.CurrentModule, snKey);

			foreach (var type in context.CurrentModule.GetTypes())
				foreach (var method in type.Methods) {
					token.ThrowIfCancellationRequested();

					if (method.Body != null) {
						method.Body.Instructions.SimplifyMacros(method.Body.Variables, method.Parameters);
					}
				}
		}

		static void ProcessModule(ConfuserContext context, CancellationToken token) { }

		static void OptimizeMethods(ConfuserContext context, CancellationToken token) {
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

		static void EndModule(ConfuserContext context, CancellationToken token) {
			string output = context.Modules[context.CurrentModuleIndex].Location;
			if (output != null) {
				if (!Path.IsPathRooted(output))
					output = Path.Combine(Environment.CurrentDirectory, output);
				output = Utils.GetRelativePath(output, context.BaseDirectory);
			}
			else {
				output = context.CurrentModule.Name;
			}
			context.OutputPaths = context.OutputPaths.SetItem(context.CurrentModuleIndex, output);
		}

		static void WriteModule(ConfuserContext context, CancellationToken token) {
			Debug.Assert(context != null, $"{nameof(context)} != null");

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("core");

			logger.LogInformation("Writing module '{0}'...", context.CurrentModule.Name);

			MemoryStream pdb = null, output = new MemoryStream();

			if (context.CurrentModule.PdbState != null) {
				pdb = new MemoryStream();
				context.CurrentModuleWriterOptions.WritePdb = true;
				context.CurrentModuleWriterOptions.PdbFileName = Path.ChangeExtension(Path.GetFileName(context.OutputPaths[context.CurrentModuleIndex]), "pdb");
				context.CurrentModuleWriterOptions.PdbStream = pdb;
			}

			token.ThrowIfCancellationRequested();

			if (context.CurrentModuleWriterOptions is ModuleWriterOptions)
				context.CurrentModule.Write(output, (ModuleWriterOptions)context.CurrentModuleWriterOptions);
			else
				context.CurrentModule.NativeWrite(output, (NativeModuleWriterOptions)context.CurrentModuleWriterOptions);

			token.ThrowIfCancellationRequested();

			context.CurrentModuleOutput = output.ToArray();
			if (context.CurrentModule.PdbState != null)
				context.CurrentModuleSymbol = pdb.ToArray();
		}

		static void DebugSymbols(ConfuserContext context, CancellationToken token) {
			Debug.Assert(context != null, $"{nameof(context)} != null");

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("core");

			logger.LogInformation("Finalizing...");
			for (int i = 0; i < context.OutputModules.Count; i++) {
				token.ThrowIfCancellationRequested();

				if (context.OutputSymbols[i] == null)
					continue;
				string path = Path.GetFullPath(Path.Combine(context.OutputDirectory, context.OutputPaths[i]));
				string dir = Path.GetDirectoryName(path);
				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir);
				File.WriteAllBytes(Path.ChangeExtension(path, "pdb"), context.OutputSymbols[i]);
			}
		}

		static void Pack(ConfuserContext context, CancellationToken token) {
			Debug.Assert(context != null, $"{nameof(context)} != null");

			if (context.Packer != null) {
				var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("core");
				logger.LogInformation("Packing...");
				context.Packer.Pack(context, new ProtectionParameters(context.Packer, context.Modules.OfType<IDnlibDef>().ToImmutableArray()), token);
			}
		}

		static void SaveModules(ConfuserContext context, CancellationToken token) {
			Debug.Assert(context != null, $"{nameof(context)} != null");

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("core");

			context.Resolver.Clear();

			for (int i = 0; i < context.OutputModules.Count; i++) {
				token.ThrowIfCancellationRequested();

				string path = Path.GetFullPath(Path.Combine(context.OutputDirectory, context.OutputPaths[i]));
				string dir = Path.GetDirectoryName(path);
				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir);
				logger.LogDebug("Saving to '{0}'...", path);
				File.WriteAllBytes(path, context.OutputModules[i]);
			}
		}

		/// <summary>
		///     Prints the copyright stuff and environment information.
		/// </summary>
		/// <param name="context">The working context.</param>
		static void PrintInfo(ConfuserContext context, ILogger logger) {
			if (context.PackerInitiated) {
				logger.LogInformation("Protecting packer stub...");
			}
			else {
				logger.LogInformation("{0} {1}", Version, Copyright);

				var mono = Type.GetType("Mono.Runtime");
				logger.LogInformation(
					"Running on {0}, {1}, {2} bits",
					Environment.OSVersion,
					mono == null ?
						".NET Framework v" + Environment.Version :
						mono.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null),
					IntPtr.Size * 8);
			}
		}

		static IEnumerable<string> GetFrameworkVersions() {
			// http://msdn.microsoft.com/en-us/library/hh925568.aspx

			using (RegistryKey ndpKey =
				RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, "").
							OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\")) {
				foreach (string versionKeyName in ndpKey.GetSubKeyNames()) {
					if (!versionKeyName.StartsWith("v"))
						continue;

					RegistryKey versionKey = ndpKey.OpenSubKey(versionKeyName);
					var name = (string)versionKey.GetValue("Version", "");
					string sp = versionKey.GetValue("SP", "").ToString();
					string install = versionKey.GetValue("Install", "").ToString();
					if (install == "" || sp != "" && install == "1")
						yield return versionKeyName + "  " + name;

					if (name != "")
						continue;

					foreach (string subKeyName in versionKey.GetSubKeyNames()) {
						RegistryKey subKey = versionKey.OpenSubKey(subKeyName);
						name = (string)subKey.GetValue("Version", "");
						if (name != "")
							sp = subKey.GetValue("SP", "").ToString();
						install = subKey.GetValue("Install", "").ToString();

						if (install == "")
							yield return versionKeyName + "  " + name;
						else if (install == "1")
							yield return "  " + subKeyName + "  " + name;
					}
				}
			}

			using (RegistryKey ndpKey =
				RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, "").
							OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\")) {
				if (ndpKey.GetValue("Release") == null)
					yield break;
				var releaseKey = (int)ndpKey.GetValue("Release");
				yield return "v4.5 " + releaseKey;
			}
		}

		/// <summary>
		///     Prints the environment information when error occurred.
		/// </summary>
		/// <param name="context">The working context.</param>
		static void PrintEnvironmentInfo(ConfuserContext context, ILogger logger) {
			if (context.PackerInitiated)
				return;

			var buildMsg = new StringBuilder();

			buildMsg.AppendLine("---BEGIN DEBUG INFO---");
			buildMsg.AppendLine("Installed Framework Versions:");
			foreach (string ver in GetFrameworkVersions()) {
				buildMsg.AppendFormat("    {0}", ver.Trim()).AppendLine();
			}
			buildMsg.AppendLine();

			if (context.Resolver != null) {
				buildMsg.AppendLine("Cached assemblies:");
				foreach (var asm in context.Resolver.GetCachedAssemblies()) {
					if (string.IsNullOrEmpty(asm.ManifestModule.Location))
						buildMsg.AppendFormat("    {0}", asm.FullName).AppendLine();
					else
						buildMsg.AppendFormat("    {0} ({1})", asm.FullName, asm.ManifestModule.Location).AppendLine();
					foreach (var reference in asm.Modules.OfType<ModuleDefMD>().SelectMany(m => m.GetAssemblyRefs()))
						buildMsg.AppendFormat("        {0}", reference.FullName).AppendLine();
				}
			}

			buildMsg.AppendLine("---END DEBUG INFO---");
			logger.LogDebug(buildMsg.ToString());
		}
	}
}
