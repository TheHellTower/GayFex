using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Confuser.Core.Project;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MethodSemanticsAttributes = dnlib.DotNet.MethodSemanticsAttributes;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Core {
	using Rules = Dictionary<Rule, IPattern>;

	/// <summary>
	/// Obfuscation Attribute Marker
	/// </summary>
	/// <inheritdoc />
	public partial class ObfAttrMarker : Marker {
		private struct ObfuscationAttributeInfo {
			public IHasCustomAttribute Owner;
			public bool? ApplyToMembers;
			public bool? Exclude;
			public string FeatureName;
			public string FeatureValue;
		}

		private struct ProtectionSettingsInfo {
			public bool ApplyToMember;
			public bool Exclude;

			public IPattern Condition;
			public string Settings;
		}

		private static readonly Regex FeaturePattern =
			new Regex("^(?:(\\d+)\\.\\s+)?([^:]+)(?:\\:(.+))?$", RegexOptions.CultureInvariant);

		private static IEnumerable<ObfuscationAttributeInfo> ReadObfuscationAttributes(IHasCustomAttribute item,
			ILogger logger) {
			var ret = new List<(int? Order, ObfuscationAttributeInfo Info)>();
			for (int i = item.CustomAttributes.Count - 1; i >= 0; i--) {
				var ca = item.CustomAttributes[i];
				if (ca.TypeFullName != typeof(ObfuscationAttribute).FullName)
					continue;

				var (info, order, strip) = CreateObfuscationAttributeInfo(item, ca, logger);
				if (strip)
					item.CustomAttributes.RemoveAt(i);

				if (!(item is ITypeDefOrRef))
					info.ApplyToMembers = false;

				ret.Add((order, info));
			}

			ret.Sort((x, y) => Nullable.Compare(x.Order, y.Order));
			return ret.Select(pair => pair.Info);
		}

		private static (ObfuscationAttributeInfo Info, int? Order, bool Strip) CreateObfuscationAttributeInfo(
			IHasCustomAttribute owner, ICustomAttribute ca, ILogger logger) {
			Debug.Assert(ca.TypeFullName == typeof(ObfuscationAttribute).FullName);

			var info = new ObfuscationAttributeInfo();
			int? order = null;

			info.Owner = owner;
			bool strip = true;
			foreach (var prop in ca.Properties) {
				switch (prop.Name) {
					case nameof(ObfuscationAttribute.ApplyToMembers):
						Debug.Assert(prop.Type.ElementType == ElementType.Boolean);
						info.ApplyToMembers = (bool)prop.Value;
						break;

					case nameof(ObfuscationAttribute.Exclude):
						Debug.Assert(prop.Type.ElementType == ElementType.Boolean);
						info.Exclude = (bool)prop.Value;
						break;

					case nameof(ObfuscationAttribute.StripAfterObfuscation):
						Debug.Assert(prop.Type.ElementType == ElementType.Boolean);
						strip = (bool)prop.Value;
						break;

					case nameof(ObfuscationAttribute.Feature):
						Debug.Assert(prop.Type.ElementType == ElementType.String);
						string feature = prop.Value as string ?? (prop.Value as UTF8String)?.String;

						(info.FeatureName, info.FeatureValue, order) = ParseObfAttrFeatureValue(feature);
						break;

					default:
						logger.LogError("Unexpected property in obfuscation attribute: ", prop.Name);
						break;
				}
			}

			if (owner is IMemberRef && !(owner is ITypeDefOrRef))
				info.ApplyToMembers = false;
			return (info, order, strip);
		}

		internal static (string FeatureName, string FeatureValue, int? Order) ParseObfAttrFeatureValue(string value) {
			var match = FeaturePattern.Match(value);

			if (!match.Success) return default;

			int? order = null;
			string featureName;
			string featureValue;
			if (match.Groups[1].Success) {
				var orderStr = match.Groups[1].Value;
				if (int.TryParse(orderStr, out int o))
					order = o;
			}

			if (match.Groups[3].Success) {
				featureName = match.Groups[2].Value;
				featureValue = match.Groups[3].Value;
			}
			else {
				featureName = "";
				featureValue = match.Groups[2].Value;
			}

			return (featureName, featureValue, order);
		}

		private bool ToInfo(IConfuserContext context, ObfuscationAttributeInfo attr, out ProtectionSettingsInfo info) {
			info = new ProtectionSettingsInfo {
				Condition = null,

				Exclude = (attr.Exclude ?? true),
				ApplyToMember = (attr.ApplyToMembers ?? true),
				Settings = attr.FeatureValue
			};

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("core");

			logger.LogTrace("Parsing settings attribute: '{0}'", info.Settings);
			bool ok = ObfAttrParser.TryParse(protections, info.Settings, logger);
			if (!ok) {
				logger.LogWarning("Ignoring rule '{0}' in {1}.", info.Settings, attr.Owner);
				return false;
			}

			if (!string.IsNullOrEmpty(attr.FeatureName))
				throw new ArgumentException("Feature name must not be set. Owner=" + attr.Owner);
			if (info.Exclude && (!string.IsNullOrEmpty(attr.FeatureName) || !string.IsNullOrEmpty(attr.FeatureValue))) {
				throw new ArgumentException("Feature property cannot be set when Exclude is true. Owner=" + attr.Owner);
			}

			return true;
		}

		private ProtectionSettingsInfo ToInfo(Rule rule, IPattern expr) {
			var info = new ProtectionSettingsInfo();

			info.Condition = expr;

			info.Exclude = false;
			info.ApplyToMember = true;

			var settings = new StringBuilder();
			if (rule.Preset != ProtectionPreset.None)
				settings.AppendFormat("preset({0});", rule.Preset.ToString().ToLowerInvariant());
			foreach (var item in rule) {
				settings.Append(item.Action == SettingItemAction.Add ? '+' : '-');
				settings.Append(item.Id);
				if (item.Count > 0) {
					settings.Append('(');
					int i = 0;
					foreach (var arg in item) {
						if (i != 0)
							settings.Append(',');
						settings.AppendFormat("{0}='{1}'", arg.Key, arg.Value.Replace("'", "\\'"));
						i++;
					}

					settings.Append(')');
				}

				settings.Append(';');
			}

			info.Settings = settings.ToString();

			return info;
		}

		private IEnumerable<ProtectionSettingsInfo> ReadInfos(IHasCustomAttribute item, IConfuserContext context,
			ILogger logger) {
			foreach (var attr in ReadObfuscationAttributes(item, logger)) {
				if (!string.IsNullOrEmpty(attr.FeatureName))
					yield return AddRule(context, attr, null);
				else if (ToInfo(context, attr, out var info))
					yield return info;
			}
		}

		private static readonly object ModuleSettingsKey = new object();

		/// <inheritdoc />
		protected internal override void MarkMember(IDnlibDef member, IConfuserContext context) {
			if (member == null) throw new ArgumentNullException(nameof(member));
			if (context == null) throw new ArgumentNullException(nameof(context));

			var module = (member as IMemberRef)?.Module;
			if (module == null) return;
			var stack = context.Annotations.Get<ProtectionSettingsStack>(module, ModuleSettingsKey);

			stack?.Apply(member);
		}

		/// <inheritdoc />
		protected internal override MarkerResult MarkProject(ConfuserProject proj, ConfuserContext context,
			CancellationToken token) {
			//this.context = context ?? throw new ArgumentNullException(nameof(context));
			var project = proj ?? throw new ArgumentNullException(nameof(proj));
			var extModules = ImmutableArray.CreateBuilder<ReadOnlyMemory<byte>>();

			IPacker packer = null;
			IDictionary<string, string> packerParams = null;

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("core");

			if (proj.Packer != null) {
				if (!packers.ContainsKey(proj.Packer.Id)) {
					logger.LogCritical("Cannot find packer with ID '{0}'.", proj.Packer.Id);
					throw new ConfuserException();
				}

				packer = packers[proj.Packer.Id];
				packerParams = new Dictionary<string, string>(proj.Packer, StringComparer.OrdinalIgnoreCase);
			}

			var modules = new List<(ProjectModule ProjModule, ModuleDefMD ModuleDef)>();
			foreach (var module in proj) {
				if (module.IsExternal) {
					extModules.Add(module.LoadRaw(proj.BaseDirectory));
					continue;
				}

				try {
					var modDef = module.Resolve(proj.BaseDirectory, context.InternalResolver.DefaultModuleContext);
					foreach (var method in modDef.FindDefinitions().OfType<MethodDef>()) {
						logger.LogTrace("Loading custom debug infos for '{0}'.", method);
						logger.LogTrace(
							method.HasCustomDebugInfos
								? "Custom debug infos for '{0}' loaded."
								: "Method '{0}' has no custom debug infos.", method);
						token.ThrowIfCancellationRequested();
					}

					token.ThrowIfCancellationRequested();

					context.InternalResolver.AddToCache(modDef);
					modules.Add((module, modDef));
				}
				catch (BadImageFormatException ex) {
					logger.LogError("Failed to load \"{0}\" - Assembly does not appear to be a .NET assembly: \"{1}\"", module.Path, ex.Message);
					throw new ConfuserException(ex);
				}
			}

			foreach (var module in modules) {
				logger.LogInformation("Loading '{0}'...", module.ProjModule.Path);

				var rules = ParseRules(proj, module.ProjModule, context);
				MarkModule(context, project, module.ProjModule, module.ModuleDef, rules, module == modules[0], logger,
					extModules, ref packer, ref packerParams);

				context.Annotations.Set(module.ModuleDef, RulesKey, rules);

				// Packer parameters are stored in modules
				if (packer != null)
					ProtectionParameters.GetParameters(context, module.ModuleDef)[packer] = packerParams;
			}

			if (proj.Debug && proj.Packer != null)
				logger.LogWarning("Generated Debug symbols might not be usable with packers!");

			return new MarkerResult(modules.Select(module => module.ModuleDef).ToImmutableArray(), packer,
				extModules.ToImmutable());
		}

		private ProtectionSettingsInfo AddRule(IConfuserContext context, ObfuscationAttributeInfo attr,
			ICollection<ProtectionSettingsInfo> infos) {
			Debug.Assert(attr.FeatureName != null);

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("core");

			var pattern = attr.FeatureName;
			IPattern expr;
			try {
				expr = PatternParser.Parse(pattern, logger);
			}
			catch (Exception ex) {
				throw new Exception(
					"Error when parsing pattern " + pattern + " in ObfuscationAttribute. Owner=" + attr.Owner, ex);
			}

			var info = new ProtectionSettingsInfo {
				Condition = expr,

				Exclude = (attr.Exclude ?? true),
				ApplyToMember = (attr.ApplyToMembers ?? true),
				Settings = attr.FeatureValue
			};

			logger.LogTrace("Parsing settings attribute: '{0}'", info.Settings);
			bool ok = ObfAttrParser.TryParse(protections, info.Settings, logger);

			if (!ok)
				logger.LogWarning("Ignoring rule '{0}' in {1}.", info.Settings, attr.Owner);
			else
				infos?.Add(info);

			return info;
		}

		private void MarkModule(IConfuserContext context, ConfuserProject project, ProjectModule projModule,
			ModuleDefMD module, Rules rules, bool isMain, ILogger logger, ICollection<ReadOnlyMemory<byte>> extModules,
			ref IPacker packer, ref IDictionary<string, string> packerParams) {
			string snKeyPath = projModule.SNKeyPath;
			string snKeyPass = projModule.SNKeyPassword;
			string snPubKeyPath = projModule.SNPubKeyPath;
			bool snDelaySig = projModule.SNDelaySig;
			string snSigKeyPath = projModule.SNSigKeyPath;
			string snSigKeyPass = projModule.SNSigKeyPassword;
			string snPubSigKeyPath = projModule.SNPubSigKeyPath;

			var stack = new ProtectionSettingsStack(context, protections);

			var layer = new List<ProtectionSettingsInfo>();
			// Add rules
			foreach (var rule in rules)
				layer.Add(ToInfo(rule.Key, rule.Value));

			// Add obfuscation attributes
			foreach (var attr in ReadObfuscationAttributes(module.Assembly, logger)) {
				if (string.IsNullOrEmpty(attr.FeatureName) && ToInfo(context, attr, out var info)) {
					layer.Add(info);
				}
				else if (string.Equals(attr.FeatureName, "generate debug symbol", StringComparison.OrdinalIgnoreCase)) {
					if (!isMain)
						throw new ArgumentException("Only main module can set 'generate debug symbol'.");
					project.Debug = bool.Parse(attr.FeatureValue);
				}
				else if (string.Equals(attr.FeatureName, "random seed", StringComparison.OrdinalIgnoreCase)) {
					if (!isMain)
						throw new ArgumentException("Only main module can set 'random seed'.");
					project.Seed = attr.FeatureValue;
				}
				else if (string.Equals(attr.FeatureName, "strong name key", StringComparison.OrdinalIgnoreCase)) {
					snKeyPath = Path.Combine(project.BaseDirectory, attr.FeatureValue);
				}
				else if (string.Equals(attr.FeatureName, "strong name key password",
					StringComparison.OrdinalIgnoreCase)) {
					snKeyPass = attr.FeatureValue;
				}
				else if (string.Equals(attr.FeatureName, "packer", StringComparison.OrdinalIgnoreCase)) {
					if (!isMain)
						throw new ArgumentException("Only main module can set 'packer'.");
					(packer, packerParams) = ObfAttrParser.ParsePacker(packers, attr.FeatureValue, logger);
				}
				else if (string.Equals(attr.FeatureName, "external module", StringComparison.OrdinalIgnoreCase)) {
					if (!isMain)
						throw new ArgumentException("Only main module can add external modules.");
					var rawModule = new ProjectModule { Path = attr.FeatureValue }.LoadRaw(project.BaseDirectory);
					extModules.Add(rawModule);
				}
				else {
					AddRule(context, attr, layer);
				}
			}

			if (project.Debug && module.PdbState == null) {
				module.LoadPdb();
			}

			snKeyPath = snKeyPath == null ? null : Path.Combine(project.BaseDirectory, snKeyPath);
			snPubKeyPath = snPubKeyPath == null ? null : Path.Combine(project.BaseDirectory, snPubKeyPath);
			snSigKeyPath = snSigKeyPath == null ? null : Path.Combine(project.BaseDirectory, snSigKeyPath);
			snPubSigKeyPath = snPubSigKeyPath == null ? null : Path.Combine(project.BaseDirectory, snPubSigKeyPath);

			var snKey = LoadSNKey(context, snKeyPath, snKeyPass);
			context.Annotations.Set(module, SNKey, snKey);

			var snPubKey = LoadSNPubKey(context, snPubKeyPath);
			context.Annotations.Set(module, SNPubKey, snPubKey);

			context.Annotations.Set(module, SNDelaySig, snDelaySig);

			var snSigKey = LoadSNKey(context, snSigKeyPath, snSigKeyPass);
			context.Annotations.Set(module, SNSigKey, snSigKey);

			var snSigPubKey = LoadSNPubKey(context, snPubSigKeyPath);
			context.Annotations.Set(module, SNSigPubKey, snSigPubKey);

			using (stack.Apply(module, layer))
				ProcessModule(module, stack, context, logger);
		}

		private void ProcessModule(ModuleDef module, ProtectionSettingsStack stack, IConfuserContext context,
			ILogger logger) {
			context.Annotations.Set(module, ModuleSettingsKey, new ProtectionSettingsStack(stack));
			foreach (var type in module.Types)
				ProcessTypeMembers(type, stack, context, logger);
		}

		private void ProcessTypeMembers(TypeDef type, ProtectionSettingsStack stack, IConfuserContext context,
			ILogger logger) {
			using (stack.Apply(type, ReadInfos(type, context, logger))) {
				foreach (var nestedType in type.NestedTypes)
					ProcessTypeMembers(nestedType, stack, context, logger);

				foreach (var property in type.Properties)
					ProcessMember(property, stack, context, logger);

				foreach (var evt in type.Events)
					ProcessMember(evt, stack, context, logger);

				foreach (var method in type.Methods) {
					if (method.SemanticsAttributes == MethodSemanticsAttributes.None)
						ProcessMember(method, stack, context, logger);
				}

				foreach (var field in type.Fields)
					ProcessMember(field, stack, context, logger);
			}
		}

		private void ProcessMember(PropertyDef property, ProtectionSettingsStack stack, IConfuserContext context,
			ILogger logger) {
			using (stack.Apply(property, ReadInfos(property, context, logger))) {
				if (property.GetMethod != null)
					ProcessMember(property.GetMethod, stack, context, logger);

				if (property.SetMethod != null)
					ProcessMember(property.SetMethod, stack, context, logger);

				foreach (var m in property.OtherMethods)
					ProcessMember(m, stack, context, logger);
			}
		}

		private void ProcessMember(EventDef evt, ProtectionSettingsStack stack, IConfuserContext context,
			ILogger logger) {
			using (stack.Apply(evt, ReadInfos(evt, context, logger))) {
				if (evt.AddMethod != null)
					ProcessMember(evt.AddMethod, stack, context, logger);

				if (evt.RemoveMethod != null)
					ProcessMember(evt.RemoveMethod, stack, context, logger);

				if (evt.InvokeMethod != null)
					ProcessMember(evt.InvokeMethod, stack, context, logger);

				foreach (var m in evt.OtherMethods)
					ProcessMember(m, stack, context, logger);
			}
		}

		private void ProcessMember(MethodDef method, ProtectionSettingsStack stack, IConfuserContext context,
			ILogger logger) {
			using (stack.Apply(method, ReadInfos(method, context, logger)))
				ProcessBody(method, stack, context, logger);
		}

		private void ProcessMember(IDnlibDef method, ProtectionSettingsStack stack, IConfuserContext context,
			ILogger logger) =>
			stack.Apply(method, ReadInfos(method, context, logger)).Dispose();

		private void ProcessBody(MethodDef method, ProtectionSettingsStack stack, IConfuserContext context,
			ILogger logger) {
			if (method?.Body == null)
				return;

			var declType = method.DeclaringType;
			foreach (var instr in method.Body.Instructions)
				if (instr.Operand is MethodDef targetMethod) {
					var cgType = targetMethod.DeclaringType;

					// Check if this is a lambda method.
					if (cgType.DeclaringType == declType && cgType.IsCompilerGenerated()) {
						using (stack.Apply(cgType, ReadInfos(cgType, context, logger)))
							ProcessTypeMembers(cgType, stack, context, logger);
					}
				}
		}
	}
}
