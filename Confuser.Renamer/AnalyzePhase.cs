using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Renamer.Analyzers;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Renamer {
	internal sealed class AnalyzePhase : IProtectionPhase {
		public AnalyzePhase(NameProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public NameProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public bool ProcessAll => true;

		public ProtectionTargets Targets => ProtectionTargets.AllDefinitions;

		public string Name => "Name analysis";

		private void ParseParameters(IConfuserContext context, IDnlibDef def, INameService service,
			IProtectionParameters parameters) {
			var mode = parameters.GetParameter(context, def, Parent.Parameters.Mode);
			service.SetRenameMode(context, def, mode);
		}

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters,
			CancellationToken token) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (parameters == null) throw new ArgumentNullException(nameof(parameters));

			var service = (NameService)context.Registry.GetRequiredService<INameService>();
			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(NameProtection._Id);
			logger.LogDebug("Building VTables & identifier list...");

			foreach (ModuleDef moduleDef in parameters.Targets.OfType<ModuleDef>())
				moduleDef.EnableTypeDefFindCache = true;

			foreach (IDnlibDef def in parameters.Targets /*.WithProgress(logger)*/) {
				ParseParameters(context, def, service, parameters);

				if (def is ModuleDef module) {
					foreach (var res in module.Resources)
						service.AddReservedIdentifier(res.Name);
				}
				else {
					service.StoreNames(context, def);
				}

				if (def is TypeDef typeDef) {
					service.GetVTable(typeDef);
				}

				token.ThrowIfCancellationRequested();
			}

			logger.LogDebug("Analyzing...");
			RegisterRenamers(context, service, logger);
			var renamers = service.Renamers;
			foreach (IDnlibDef def in parameters.Targets /*.WithProgress(logger)*/) {
				Analyze(service, context, parameters, def, true);
				token.ThrowIfCancellationRequested();
			}

			foreach (ModuleDef moduleDef in parameters.Targets.OfType<ModuleDef>()) {
				moduleDef.EnableTypeDefFindCache = false;
				moduleDef.ResetTypeDefFindCache();
			}
		}

		void RegisterRenamers(IConfuserContext context, NameService service, ILogger logger) {
			bool wpf = false;
			bool caliburn = false;
			bool winforms = false;
			bool json = false;
			bool visualBasic = false;
			bool vsComposition = false;

			foreach (var module in context.Modules) {
				foreach (var asmRef in module.GetAssemblyRefs()) {
					if (asmRef.Name == "WindowsBase" || asmRef.Name == "PresentationCore" ||
					    asmRef.Name == "PresentationFramework" || asmRef.Name == "System.Xaml") {
						wpf = true;
					}
					else if (asmRef.Name == "Caliburn.Micro") {
						caliburn = true;
					}
					else if (asmRef.Name == "System.Windows.Forms") {
						winforms = true;
					}
					else if (asmRef.Name == "Newtonsoft.Json") {
						json = true;
					}
					else if (asmRef.Name == "Microsoft.VisualStudio.Composition") {
						vsComposition = true;
					}
				}

				var vbEmbeddedAttribute = module.FindNormal("Microsoft.VisualBasic.Embedded");
				if (vbEmbeddedAttribute != null && vbEmbeddedAttribute.BaseType.FullName.Equals("System.Attribute")) {
					visualBasic = true;
				}
			}

			if (wpf) {
				var wpfAnalyzer = new WPFAnalyzer(Parent);
				logger.LogDebug("WPF found, enabling compatibility.");
				service.RegisterRenamer(wpfAnalyzer);
				if (caliburn) {
					logger.LogDebug("Caliburn.Micro found, enabling compatibility.");
					service.RegisterRenamer(new CaliburnAnalyzer(context, wpfAnalyzer));
				}
			}

			if (winforms) {
				var winformsAnalyzer = new WinFormsAnalyzer();
				logger.LogDebug("WinForms found, enabling compatibility.");
				service.RegisterRenamer(winformsAnalyzer);
			}

			if (json) {
				var jsonAnalyzer = new JsonAnalyzer(Parent);
				logger.LogDebug("Newtonsoft.Json found, enabling compatibility.");
				service.RegisterRenamer(jsonAnalyzer);
			}

			if (visualBasic) {
				var vbAnalyzer = new VisualBasicRuntimeAnalyzer();
				logger.LogDebug("Visual Basic Embedded Runtime found, enabling compatibility.");
				service.RegisterRenamer(vbAnalyzer);
			}

			if (vsComposition) {
				var analyzer = new VsCompositionAnalyzer();
				logger.LogDebug("Visual Studio Composition found, enabling compatibility.");
				service.Renamers.Add(analyzer);
			}
		}

		internal void Analyze(NameService service, IConfuserContext context, IProtectionParameters parameters,
			IDnlibDef def, bool runAnalyzer) {
			if (def is TypeDef)
				Analyze(service, context, parameters, (TypeDef)def);
			else if (def is MethodDef)
				Analyze(service, context, parameters, (MethodDef)def);
			else if (def is FieldDef)
				Analyze(service, context, parameters, (FieldDef)def);
			else if (def is PropertyDef)
				Analyze(service, context, parameters, (PropertyDef)def);
			else if (def is EventDef)
				Analyze(service, context, parameters, (EventDef)def);
			else if (def is ModuleDef) {
				var renamingMode = parameters.GetParameter(context, def, Parent.Parameters.Mode);
				if (renamingMode == RenameMode.Reversible && service.reversibleRenamer == null) {
					var generatePassword = parameters.GetParameter(context, def, Parent.Parameters.GeneratePassword);
					var password = parameters.GetParameter(context, def, Parent.Parameters.Password);
					if (generatePassword || password == null) {
						password = context.Registry.GetService<IRandomService>().SeedString;
					}

					string dir = context.OutputDirectory;
					string path = Path.GetFullPath(Path.Combine(dir, CoreConstants.PasswordFileName));
					if (!Directory.Exists(dir))
						Directory.CreateDirectory(dir);
					File.WriteAllText(path, password);
					service.reversibleRenamer = new ReversibleRenamer(password);
				}

				var idOffset = parameters.GetParameter(context, def, Parent.Parameters.IdOffset);
				if (idOffset != 0)
					service.SetNameId(idOffset);

				service.SetCanRename(context, def, false);
			}

			if (!runAnalyzer || parameters.GetParameter(context, def, Parent.Parameters.ForceRename))
				return;

			foreach (var renamer in service.Renamers)
				renamer.Analyze(context, service, parameters, def);
		}

		void Analyze(INameService service, IConfuserContext context, IProtectionParameters parameters, TypeDef type) {
			if (type.IsRuntimeSpecialName || type.IsGlobalModuleType) {
				service.SetCanRename(context, type, false);
			}

			if (parameters.GetParameter(context, type, Parent.Parameters.ForceRename))
				return;

			if (type.InheritsFromCorlib("System.Attribute")) {
				service.ReduceRenameMode(context, type, RenameMode.Reflection);
			}

			if (type.InheritsFrom("System.Configuration.SettingsBase")) {
				service.SetCanRename(context, type, false);
			}
		}

		void Analyze(INameService service, IConfuserContext context, IProtectionParameters parameters,
			MethodDef method) {
			if (method.IsRuntimeSpecialName)
				service.SetCanRename(context, method, false);

			else if (method.IsExplicitlyImplementedInterfaceMember())
				service.SetCanRename(context, method, false);

			else if (parameters.GetParameter(context, method, Parent.Parameters.ForceRename))
				return;

			else if (method.DeclaringType.IsComImport() &&
			         !method.HasAttribute("System.Runtime.InteropServices.DispIdAttribute"))
				service.SetCanRename(context, method, false);

			else if (method.DeclaringType.IsDelegate())
				service.SetCanRename(context, method, false);
		}

		void Analyze(INameService service, IConfuserContext context, IProtectionParameters parameters, FieldDef field) {
			if (field.IsRuntimeSpecialName)
				service.SetCanRename(context, field, false);

			else if (parameters.GetParameter(context, field, Parent.Parameters.ForceRename))
				return;

			else if (field.DeclaringType.IsSerializable && !field.IsNotSerialized)
				service.SetCanRename(context, field, false);

			else if (field.IsLiteral && field.DeclaringType.IsEnum &&
			         !parameters.GetParameter(context, field, Parent.Parameters.ForceRename))
				service.SetCanRename(context, field, false);
		}

		void Analyze(INameService service, IConfuserContext context, IProtectionParameters parameters,
			PropertyDef property) {
			if (property.IsRuntimeSpecialName)
				service.SetCanRename(context, property, false);

			else if (parameters.GetParameter(context, property, Parent.Parameters.ForceRename))
				return;

			else if (property.DeclaringType.Implements("System.ComponentModel.INotifyPropertyChanged"))
				service.SetCanRename(context, property, false);

			else if (property.DeclaringType.Name.String.Contains("AnonymousType"))
				service.SetCanRename(context, property, false);
		}

		void Analyze(INameService service, IConfuserContext context, IProtectionParameters parameters, EventDef evt) {
			if (evt.DeclaringType.IsVisibleOutside() && evt.IsVisibleOutside())
				service.SetCanRename(context, evt, false);

			else if (evt.IsRuntimeSpecialName)
				service.SetCanRename(context, evt, false);
		}
	}
}
