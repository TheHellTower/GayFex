using System;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Renamer.Analyzers;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;
using ILogger = Confuser.Core.ILogger;

namespace Confuser.Renamer {
	internal sealed class AnalyzePhase : IProtectionPhase {
		public AnalyzePhase(NameProtection parent) => 
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public NameProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public bool ProcessAll => true;

		public ProtectionTargets Targets => ProtectionTargets.AllDefinitions;

		public string Name => "Name analysis";

		private void ParseParameters(IConfuserContext context, IDnlibDef def, INameService service, IProtectionParameters parameters)
		{
			var mode = parameters.GetParameter<RenameMode?>(context, def, "mode", null);
			if (mode != null)
				service.SetRenameMode(context, def, mode.Value);
		}

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (parameters == null) throw new ArgumentNullException(nameof(parameters));

			var service = (NameService)context.Registry.GetRequiredService<ILoggingService>().GetLogger("naming");
			var logger = context.Registry.GetRequiredService<ILogger>();
			logger.Debug("Building VTables & identifier list...");
			foreach (IDnlibDef def in parameters.Targets.WithProgress(logger)) {
				ParseParameters(context, def, service, parameters);

				if (def is ModuleDef module) {
					foreach (var res in module.Resources)
						service.SetOriginalName(context, res, res.Name);
				}
				else
					service.SetOriginalName(context, def, def.Name);

				if (def is TypeDef) {
					service.GetVTables().GetVTable((TypeDef)def);
					service.SetOriginalNamespace(context, def, ((TypeDef)def).Namespace);
				}
				token.ThrowIfCancellationRequested();
			}

			logger.Debug("Analyzing...");
			RegisterRenamers(context, service, logger);
			var renamers = service.Renamers;
			foreach (IDnlibDef def in parameters.Targets.WithProgress(logger)) {
				Analyze(service, context, parameters, def, true);
				token.ThrowIfCancellationRequested();
			}
		}

		void RegisterRenamers(IConfuserContext context, NameService service, ILogger logger) {
			bool wpf = false,
			     caliburn = false,
			     winforms = false,
			     json = false;

			foreach (var module in context.Modules)
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
				}

			if (wpf) {
				var wpfAnalyzer = new WPFAnalyzer();
				logger.Debug("WPF found, enabling compatibility.");
				service.Renamers.Add(wpfAnalyzer);
				if (caliburn) {
					logger.Debug("Caliburn.Micro found, enabling compatibility.");
					service.Renamers.Add(new CaliburnAnalyzer(context, wpfAnalyzer));
				}
			}

			if (winforms) {
				var winformsAnalyzer = new WinFormsAnalyzer();
				logger.Debug("WinForms found, enabling compatibility.");
				service.Renamers.Add(winformsAnalyzer);
			}

			if (json) {
				var jsonAnalyzer = new JsonAnalyzer();
				logger.Debug("Newtonsoft.Json found, enabling compatibility.");
				service.Renamers.Add(jsonAnalyzer);
			}
		}

		internal void Analyze(NameService service, IConfuserContext context, IProtectionParameters parameters, IDnlibDef def, bool runAnalyzer) {
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
				var pass = parameters.GetParameter<string>(context, def, "password", null);
				if (pass != null)
					service.reversibleRenamer = new ReversibleRenamer(pass);

				var idOffset = parameters.GetParameter<uint>(context, def, "idOffset", 0);
				if (idOffset != 0)
					service.SetNameId(idOffset);

				service.SetCanRename(context, def, false);
			}

			if (!runAnalyzer || parameters.GetParameter(context, def, "forceRen", false))
				return;

			foreach (var renamer in service.Renamers)
				renamer.Analyze(context, service, parameters, def);
		}

		static bool IsVisibleOutside(IConfuserContext context, IProtectionParameters parameters, IMemberDef def) {
			var type = def as TypeDef;
			if (type == null)
				type = def.DeclaringType;

			var renPublic = parameters.GetParameter<bool?>(context, def, "renPublic", null);
			if (renPublic == null)
				return type.IsVisibleOutside();
			else
				return type.IsVisibleOutside(false) && !renPublic.Value;
		}

		void Analyze(INameService service, IConfuserContext context, IProtectionParameters parameters, TypeDef type) {
			if (IsVisibleOutside(context, parameters, type)) {
				service.SetCanRename(context, type, false);
			}
			else if (type.IsRuntimeSpecialName || type.IsGlobalModuleType) {
				service.SetCanRename(context, type, false);
			}
			else if (type.FullName == "ConfusedByAttribute") {
				// Courtesy
				service.SetCanRename(context, type, false);
			}

			if (parameters.GetParameter(context, type, "forceRen", false))
				return;

			if (type.InheritsFromCorlib("System.Attribute")) {
				service.ReduceRenameMode(context, type, RenameMode.ASCII);
			}

			if (type.InheritsFrom("System.Configuration.SettingsBase")) {
				service.SetCanRename(context, type, false);
			}
		}

		void Analyze(INameService service, IConfuserContext context, IProtectionParameters parameters, MethodDef method) {
			if (IsVisibleOutside(context, parameters, method.DeclaringType) &&
			    (method.IsFamily || method.IsFamilyOrAssembly || method.IsPublic) &&
			    IsVisibleOutside(context, parameters, method))
				service.SetCanRename(context, method, false);

			else if (method.IsRuntimeSpecialName)
				service.SetCanRename(context, method, false);

			else if (method.IsExplicitlyImplementedInterfaceMember())
				service.SetCanRename(context, method, false);

			else if (parameters.GetParameter(context, method, "forceRen", false))
				return;

			else if (method.DeclaringType.IsComImport() && !method.HasAttribute("System.Runtime.InteropServices.DispIdAttribute"))
				service.SetCanRename(context, method, false);

			else if (method.DeclaringType.IsDelegate())
				service.SetCanRename(context, method, false);
		}

		void Analyze(INameService service, IConfuserContext context, IProtectionParameters parameters, FieldDef field) {
			if (IsVisibleOutside(context, parameters, field.DeclaringType) &&
			    (field.IsFamily || field.IsFamilyOrAssembly || field.IsPublic) &&
			    IsVisibleOutside(context, parameters, field))
				service.SetCanRename(context, field, false);

			else if (field.IsRuntimeSpecialName)
				service.SetCanRename(context, field, false);

			else if (parameters.GetParameter(context, field, "forceRen", false))
				return;

			else if (field.DeclaringType.IsSerializable && !field.IsNotSerialized)
				service.SetCanRename(context, field, false);

			else if (field.IsLiteral && field.DeclaringType.IsEnum &&
				!parameters.GetParameter(context, field, "renEnum", false))
				service.SetCanRename(context, field, false);
		}

		void Analyze(INameService service, IConfuserContext context, IProtectionParameters parameters, PropertyDef property) {
			if (IsVisibleOutside(context, parameters, property.DeclaringType) &&
			    IsVisibleOutside(context, parameters, property))
				service.SetCanRename(context, property, false);

			else if (property.IsRuntimeSpecialName)
				service.SetCanRename(context, property, false);

			else if (property.IsExplicitlyImplementedInterfaceMember())
				service.SetCanRename(context, property, false);

			else if (parameters.GetParameter(context, property, "forceRen", false))
				return;

			else if (property.DeclaringType.Implements("System.ComponentModel.INotifyPropertyChanged"))
				service.SetCanRename(context, property, false);

			else if (property.DeclaringType.Name.String.Contains("AnonymousType"))
				service.SetCanRename(context, property, false);
		}

		void Analyze(INameService service, IConfuserContext context, IProtectionParameters parameters, EventDef evt) {
			if (IsVisibleOutside(context, parameters, evt.DeclaringType) &&
			    IsVisibleOutside(context, parameters, evt))
				service.SetCanRename(context, evt, false);

			else if (evt.IsRuntimeSpecialName)
				service.SetCanRename(context, evt, false);

			else if (evt.IsExplicitlyImplementedInterfaceMember())
				service.SetCanRename(context, evt, false);
		}
	}
}
