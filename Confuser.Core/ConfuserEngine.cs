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
using Microsoft.Win32;
using CopyrightAttribute = System.Reflection.AssemblyCopyrightAttribute;
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
		public static Task Run(ConfuserParameters parameters, CancellationToken? token = null) {
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
		static void RunInternal(ConfuserParameters parameters, CancellationToken token) {
			var logger = parameters.Logger;

			bool ok = false;
			try {

				Marker marker = parameters.GetMarker();

				// 2. Discover plugins
				logger.Debug("Discovering plugins...");

				var plugInContainer = parameters.GetPluginDiscovery().GetPlugins(parameters.Project, logger);
				var prots = plugInContainer.GetExports<IProtection>();
				var packers = plugInContainer.GetExports<IPacker>();
				var components = plugInContainer.GetExports<IConfuserComponent>();

				logger.InfoFormat("Discovered {0} protections, {1} packers.", prots.Count(), packers.Count());

				token.ThrowIfCancellationRequested();

				var sortedComponents = new List<IConfuserComponent>();
				sortedComponents.Add(new CoreComponent(parameters, marker));
				sortedComponents.AddRange(components.Select(l => l.Value));

				// 3. Resolve dependency
				logger.Debug("Resolving component dependency...");
				try {
					var resolver = new DependencyResolver(prots.Select(l => l.Value));
					sortedComponents.AddRange(resolver.SortDependency());
				}
				catch (CircularDependencyException ex) {
					logger.ErrorException("", ex);
					throw new ConfuserException(ex);
				}
				sortedComponents.AddRange(packers.Select(l => l.Value));

				token.ThrowIfCancellationRequested();

				// 4. Initialize components
				logger.Info("Initializing...");
				var serviceCollection = new ServiceCollection();
				foreach (var comp in sortedComponents) {
					try {
						comp.Initialize(serviceCollection);
					}
					catch (Exception ex) {
						logger.ErrorException("Error occured during initialization of '" + comp.Name + "'.", ex);
						throw new ConfuserException(ex);
					}
					token.ThrowIfCancellationRequested();
				}

				// 1. Setup context
				var context = new ConfuserContext(serviceCollection.BuildServiceProvider());
				context.Project = parameters.Project.Clone();
				context.PackerInitiated = parameters.PackerInitiated;

				PrintInfo(context);

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
					logger.Info("Loading input modules...");
					marker.Initalize(prots.Select(l => l.Value), packers.Select(l => l.Value));
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
					logger.Debug("Building pipeline...");
					var pipeline = new ProtectionPipeline();
					context.Pipeline = pipeline;
					foreach (IConfuserComponent comp in sortedComponents) {
						comp.PopulatePipeline(pipeline);
					}

					token.ThrowIfCancellationRequested();

					//7. Run pipeline
					RunPipeline(pipeline, context, token);

					ok = true;
				}
				catch (Exception) {
					PrintEnvironmentInfo(context);
					throw;
				}
			}
			catch (AssemblyResolveException ex) {
				logger.ErrorException("Failed to resolve an assembly, check if all dependencies are present in the correct version.", ex);
			}
			catch (TypeResolveException ex) {
				logger.ErrorException("Failed to resolve a type, check if all dependencies are present in the correct version.", ex);
			}
			catch (MemberRefResolveException ex) {
				logger.ErrorException("Failed to resolve a member, check if all dependencies are present in the correct version.", ex);
			}
			catch (IOException ex) {
				logger.ErrorException("An IO error occurred, check if all input/output locations are readable/writable.", ex);
			}
			catch (OperationCanceledException) {
				logger.Error("Operation cancelled.");
			}
			catch (ConfuserException) {
				// Exception is already handled/logged, so just ignore and report failure
			}
			catch (Exception ex) {
				logger.ErrorException("Unknown error occurred.", ex);
			}
			finally {
				logger.Finish(ok);
			}
		}

		/// <summary>
		///     Runs the protection pipeline.
		/// </summary>
		/// <param name="pipeline">The protection pipeline.</param>
		/// <param name="context">The context.</param>
		static void RunPipeline(ProtectionPipeline pipeline, ConfuserContext context, CancellationToken token) {
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

			pipeline.ExecuteStage(PipelineStage.Debug, Debug, () => getAllDefs(), context, token);
			pipeline.ExecuteStage(PipelineStage.Pack, Pack, () => getAllDefs(), context, token);
			pipeline.ExecuteStage(PipelineStage.SaveModules, SaveModules, () => getAllDefs(), context, token);

			if (!context.PackerInitiated)
				context.Logger.Info("Done.");
		}

		static void Inspection(ConfuserContext context, CancellationToken token) {
			context.Logger.Info("Resolving dependencies...");
			foreach (var dependency in context.Modules
											  .SelectMany(module => module.GetAssemblyRefs()
											  .Select(asmRef => Tuple.Create(asmRef, module)))) {
				token.ThrowIfCancellationRequested();

				try {
					AssemblyDef assembly = context.Resolver.ResolveThrow(dependency.Item1, dependency.Item2);
				}
				catch (AssemblyResolveException ex) {
					context.Logger.ErrorException("Failed to resolve dependency of '" + dependency.Item2.Name + "'.", ex);
					throw new ConfuserException(ex);
				}
			}

			context.Logger.Debug("Checking Strong Name...");
			foreach (ModuleDefMD module in context.Modules) {
				var snKey = context.Annotations.Get<StrongNameKey>(module, Marker.SNKey);
				if (snKey == null && module.IsStrongNameSigned)
					context.Logger.WarnFormat("[{0}] SN Key is not provided for a signed module, the output may not be working.", module.Name);
				else if (snKey != null && !module.IsStrongNameSigned)
					context.Logger.WarnFormat("[{0}] SN Key is provided for an unsigned module, the output may not be working.", module.Name);
				else if (snKey != null && module.IsStrongNameSigned &&
						 !module.Assembly.PublicKey.Data.SequenceEqual(snKey.PublicKey))
					context.Logger.WarnFormat("[{0}] Provided SN Key and signed module's public key do not match, the output may not be working.", module.Name);
			}

			var marker = context.Registry.GetService<IMarkerService>();

			context.Logger.Debug("Creating global .cctors...");
			foreach (ModuleDefMD module in context.Modules) {
				TypeDef modType = module.GlobalType;
				if (modType == null) {
					modType = new TypeDefUser("", "<Module>", null);
					modType.Attributes = TypeAttributes.AnsiClass;
					module.Types.Add(modType);
					marker.Mark(context, modType, null);
				}
				MethodDef cctor = modType.FindOrCreateStaticConstructor();
				if (!marker.IsMarked(context, cctor))
					marker.Mark(context, cctor, null);
			}

			context.Logger.Debug("Watermarking...");
			foreach (ModuleDefMD module in context.Modules) {
				TypeRef attrRef = module.CorLibTypes.GetTypeRef("System", "Attribute");
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

		static void CopyPEHeaders(PEHeadersOptions writerOptions, ModuleDefMD module) {
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

		static void BeginModule(ConfuserContext context, CancellationToken token) {
			context.Logger.InfoFormat("Processing module '{0}'...", context.CurrentModule.Name);

			context.CurrentModuleWriterOptions = new ModuleWriterOptions(context.CurrentModule);
			context.CurrentModuleWriterOptions.WriterEvent += (sender, e) => token.ThrowIfCancellationRequested();
			CopyPEHeaders(context.CurrentModuleWriterOptions.PEHeadersOptions, context.CurrentModule);

			if (!context.CurrentModule.IsILOnly || context.CurrentModule.VTableFixups != null)
				context.RequestNative();

			var snKey = context.Annotations.Get<StrongNameKey>(context.CurrentModule, Marker.SNKey);
			context.CurrentModuleWriterOptions.InitializeStrongNameSigning(context.CurrentModule, snKey);

			foreach (TypeDef type in context.CurrentModule.GetTypes())
				foreach (MethodDef method in type.Methods) {
					token.ThrowIfCancellationRequested();

					if (method.Body != null) {
						method.Body.Instructions.SimplifyMacros(method.Body.Variables, method.Parameters);
					}
				}
		}

		static void ProcessModule(ConfuserContext context, CancellationToken token) { }

		static void OptimizeMethods(ConfuserContext context, CancellationToken token) {
			foreach (TypeDef type in context.CurrentModule.GetTypes())
				foreach (MethodDef method in type.Methods) {
					token.ThrowIfCancellationRequested();

					if (method.Body != null)
						method.Body.Instructions.OptimizeMacros();
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
			context.Logger.InfoFormat("Writing module '{0}'...", context.CurrentModule.Name);

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

		static void Debug(ConfuserContext context, CancellationToken token) {
			context.Logger.Info("Finalizing...");
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
			if (context.Packer != null) {
				context.Logger.Info("Packing...");
				context.Packer.Pack(context, new ProtectionParameters(context.Packer, context.Modules.OfType<IDnlibDef>().ToImmutableArray()), token);
			}
		}

		static void SaveModules(ConfuserContext context, CancellationToken token) {
			context.Resolver.Clear();
			for (int i = 0; i < context.OutputModules.Count; i++) {
				token.ThrowIfCancellationRequested();

				string path = Path.GetFullPath(Path.Combine(context.OutputDirectory, context.OutputPaths[i]));
				string dir = Path.GetDirectoryName(path);
				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir);
				context.Logger.DebugFormat("Saving to '{0}'...", path);
				File.WriteAllBytes(path, context.OutputModules[i]);
			}
		}

		/// <summary>
		///     Prints the copyright stuff and environment information.
		/// </summary>
		/// <param name="context">The working context.</param>
		static void PrintInfo(ConfuserContext context) {
			if (context.PackerInitiated) {
				context.Logger.Info("Protecting packer stub...");
			}
			else {
				context.Logger.InfoFormat("{0} {1}", Version, Copyright);

				Type mono = Type.GetType("Mono.Runtime");
				context.Logger.InfoFormat("Running on {0}, {1}, {2} bits",
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
		static void PrintEnvironmentInfo(ConfuserContext context) {
			if (context.PackerInitiated)
				return;

			context.Logger.Error("---BEGIN DEBUG INFO---");

			context.Logger.Error("Installed Framework Versions:");
			foreach (string ver in GetFrameworkVersions()) {
				context.Logger.ErrorFormat("    {0}", ver.Trim());
			}
			context.Logger.Error("");

			if (context.Resolver != null) {
				context.Logger.Error("Cached assemblies:");
				foreach (AssemblyDef asm in context.Resolver.GetCachedAssemblies()) {
					if (string.IsNullOrEmpty(asm.ManifestModule.Location))
						context.Logger.ErrorFormat("    {0}", asm.FullName);
					else
						context.Logger.ErrorFormat("    {0} ({1})", asm.FullName, asm.ManifestModule.Location);
					foreach (var reference in asm.Modules.OfType<ModuleDefMD>().SelectMany(m => m.GetAssemblyRefs()))
						context.Logger.ErrorFormat("        {0}", reference.FullName);
				}
			}

			context.Logger.Error("---END DEBUG INFO---");
		}
	}
}
