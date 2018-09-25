using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Renamer.BAML;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Confuser.Renamer.Analyzers {
	internal sealed class WPFAnalyzer : IRenamer {
		static readonly object BAMLKey = new object();

		internal static readonly Regex ResourceNamePattern = new Regex("^.*\\.g\\.resources$");
		internal static readonly Regex UriPattern = new Regex("^(?:PACK\\://(?:COMPONENT|APPLICATION)\\:,,,)*(?:/(.+?)(?:;V\\d+\\.\\d+\\.\\d+\\.\\d+)?;COMPONENT)*(/.+\\.[BX]AML)$");
		BAMLAnalyzer analyzer;

		internal Dictionary<string, List<IBAMLReference>> bamlRefs;
		public event Action<BAMLAnalyzer, BamlElement> AnalyzeBAMLElement;

		private NameProtection Protection { get; }

	    internal WPFAnalyzer(NameProtection protection) {
			Protection = protection ?? throw new ArgumentNullException(nameof(protection));
			bamlRefs = new Dictionary<string, List<IBAMLReference>>(StringComparer.OrdinalIgnoreCase);
		}

		public void Analyze(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def) {
			var method = def as MethodDef;
			if (method != null) {
				if (!method.HasBody)
					return;
				AnalyzeMethod(context, service, method);
			}

			var module = def as ModuleDefMD;
			if (module != null) {
				AnalyzeResources(context, service, module);
			}
		}

		public void PreRename(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def) {
			if (!(def is ModuleDefMD module) || !parameters.GetParameter(context, def, Protection.Parameters.RenameXaml))
				return;

			var wpfResInfo = context.Annotations.Get<Dictionary<string, Dictionary<string, BamlDocument>>>(module, BAMLKey);
			if (wpfResInfo == null)
				return;

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(NameProtection._Id);

			foreach (var res in wpfResInfo.Values)
				foreach (var doc in res.Values) {
					List<IBAMLReference> references;
					if (bamlRefs.TryGetValue(doc.DocumentName, out references)) {
						var newName = doc.DocumentName.ToUpperInvariant();

						#region old code

						//if (newName.EndsWith(".BAML"))
						//    newName = service.RandomName(RenameMode.Letters).ToLowerInvariant() + ".baml";
						//else if (newName.EndsWith(".XAML"))
						//    newName = service.RandomName(RenameMode.Letters).ToLowerInvariant() + ".xaml";

						#endregion

						#region Niks patch fix

						/*
						 * Nik's patch for maintaining relative paths. If the xaml file is referenced in this manner
						 * "/some.namespace;component/somefolder/somecontrol.xaml"
						 * then we want to keep the relative path and namespace intact. We should be obfuscating it like this - /some.namespace;component/somefolder/asjdjh2398498dswk.xaml
						* */

						string[] completePath = newName.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
						string newShinyName = string.Empty;
						for (int i = 0; i <= completePath.Length - 2; i++) {
							newShinyName += completePath[i].ToLowerInvariant() + "/";
						}
						if (newName.EndsWith(".BAML"))
							newName = newShinyName + service.RandomName(RenameMode.Letters).ToLowerInvariant() + ".baml";
						else if (newName.EndsWith(".XAML"))
							newName = newShinyName + service.RandomName(RenameMode.Letters).ToLowerInvariant() + ".xaml";

						logger.LogDebug("Preserving virtual paths. Replaced {0} with {1}", doc.DocumentName, newName);

						#endregion

						bool renameOk = true;
						foreach (var bamlRef in references)
							if (!bamlRef.CanRename(doc.DocumentName, newName)) {
								renameOk = false;
								break;
							}

						if (renameOk) {
							foreach (var bamlRef in references)
								bamlRef.Rename(doc.DocumentName, newName);
							doc.DocumentName = newName;
						}
					}
				}
		}

		public void PostRename(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def) {
			var module = def as ModuleDefMD;
			if (module == null)
				return;

			var wpfResInfo = context.Annotations.Get<Dictionary<string, Dictionary<string, BamlDocument>>>(module, BAMLKey);
			if (wpfResInfo == null)
				return;

			var newResources = new List<EmbeddedResource>();

			foreach (EmbeddedResource res in module.Resources.OfType<EmbeddedResource>()) {
				Dictionary<string, BamlDocument> resInfo;

				if (!wpfResInfo.TryGetValue(res.Name, out resInfo))
					continue;

				var stream = new MemoryStream();
				var writer = new ResourceWriter(stream);

				var reader = new ResourceReader(res.CreateReader().AsStream());
				IDictionaryEnumerator enumerator = reader.GetEnumerator();
				while (enumerator.MoveNext()) {
					var name = (string)enumerator.Key;
					string typeName;
					byte[] data;
					reader.GetResourceData(name, out typeName, out data);

					BamlDocument document;
					if (resInfo.TryGetValue(name, out document)) {
						var docStream = new MemoryStream();
						docStream.Position = 4;
						BamlWriter.WriteDocument(document, docStream);
						docStream.Position = 0;
						docStream.Write(BitConverter.GetBytes((int)docStream.Length - 4), 0, 4);
						data = docStream.ToArray();
						name = document.DocumentName;
					}

					writer.AddResourceData(name, typeName, data);
				}
				writer.Generate();
				newResources.Add(new EmbeddedResource(res.Name, stream.ToArray(), res.Attributes));
			}

			foreach (EmbeddedResource res in newResources) {
				int index = module.Resources.IndexOfEmbeddedResource(res.Name);
				module.Resources[index] = res;
			}
		}

		void AnalyzeMethod(IConfuserContext context, INameService service, MethodDef method) {
			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(NameProtection._Id);

			var dpRegInstrs = new List<Tuple<bool, Instruction>>();
			var routedEvtRegInstrs = new List<Instruction>();
			for (int i = 0; i < method.Body.Instructions.Count; i++) {
				Instruction instr = method.Body.Instructions[i];
				if ((instr.OpCode.Code == Code.Call || instr.OpCode.Code == Code.Callvirt)) {
					var regMethod = (IMethod)instr.Operand;

					if (regMethod.DeclaringType.FullName == "System.Windows.DependencyProperty" &&
						regMethod.Name.String.StartsWith("Register")) {
						dpRegInstrs.Add(Tuple.Create(regMethod.Name.String.StartsWith("RegisterAttached"), instr));
					}
					else if (regMethod.DeclaringType.FullName == "System.Windows.EventManager" &&
							 regMethod.Name.String == "RegisterRoutedEvent") {
						routedEvtRegInstrs.Add(instr);
					}
				}
				else if (instr.OpCode.Code == Code.Newobj) {
					var methodRef = (IMethod)instr.Operand;

					if (methodRef.DeclaringType.FullName == "System.Windows.Data.PropertyGroupDescription" &&
						methodRef.Name == ".ctor" && i - 1 >= 0 && method.Body.Instructions[i - 1].OpCode.Code == Code.Ldstr) {
						foreach (var property in analyzer.LookupProperty((string)method.Body.Instructions[i - 1].Operand))
							service.SetCanRename(context, property, false);
					}
				}
				else if (instr.OpCode == OpCodes.Ldstr) {
					var operand = ((string)instr.Operand).ToUpperInvariant();
					if (operand.EndsWith(".BAML") || operand.EndsWith(".XAML")) {
						var match = UriPattern.Match(operand);
						if (match.Success) {
							var resourceAssemblyName = match.Groups[1].Value;
							if (resourceAssemblyName != null && !resourceAssemblyName.Equals(method.Module.Assembly.Name.String, StringComparison.OrdinalIgnoreCase)) {
								// This resource points to another assembly.
								// Leave it alone!
								return;
							}
							operand = match.Groups[2].Value;
						}
						else if (operand.Contains("/"))
							logger.LogWarning("Fail to extract XAML name from '{0}'.", instr.Operand);

						var reference = new BAMLStringReference(instr);
						operand = operand.TrimStart('/');
						var baml = operand.Substring(0, operand.Length - 5) + ".BAML";
						var xaml = operand.Substring(0, operand.Length - 5) + ".XAML";
						bamlRefs.AddListEntry(baml, reference);
						bamlRefs.AddListEntry(xaml, reference);
					}
				}
			}

			if (dpRegInstrs.Count == 0)
				return;

			var traceSrv = context.Registry.GetService<ITraceService>();
			var trace = traceSrv.Trace(method);

			bool erred = false;
			foreach (var instrInfo in dpRegInstrs) {
				int[] args = trace.TraceArguments(instrInfo.Item2);
				if (args == null) {
					if (!erred)
						logger.LogWarning("Failed to extract dependency property name in '{0}'.", method.FullName);
					erred = true;
					continue;
				}
				Instruction ldstr = method.Body.Instructions[args[0]];
				if (ldstr.OpCode.Code != Code.Ldstr) {
					if (!erred)
						logger.LogWarning("Failed to extract dependency property name in '{0}'.", method.FullName);
					erred = true;
					continue;
				}

				var name = (string)ldstr.Operand;
				TypeDef declType = method.DeclaringType;
				bool found = false;
				if (instrInfo.Item1) // Attached DP
				{
					MethodDef accessor;
					if ((accessor = declType.FindMethod("Get" + name)) != null && accessor.IsStatic) {
						service.SetCanRename(context, accessor, false);
						found = true;
					}
					if ((accessor = declType.FindMethod("Set" + name)) != null && accessor.IsStatic) {
						service.SetCanRename(context, accessor, false);
						found = true;
					}
				}

				// Normal DP
				// Find CLR property for attached DP as well, because it seems attached DP can be use as normal DP as well.
				PropertyDef property = null;
				if ((property = declType.FindProperty(name)) != null) {
					service.SetCanRename(context, property, false);

					found = true;
					if (property.GetMethod != null)
						service.SetCanRename(context, property.GetMethod, false);

					if (property.SetMethod != null)
						service.SetCanRename(context, property.SetMethod, false);

					if (property.HasOtherMethods) {
						foreach (MethodDef accessor in property.OtherMethods)
							service.SetCanRename(context, accessor, false);
					}
				}
				if (!found) {
					if (instrInfo.Item1)
						logger.LogWarning("Failed to find the accessors of attached dependency property '{0}' in type '{1}'.",
												  name, declType.FullName);
					else
						logger.LogWarning("Failed to find the CLR property of normal dependency property '{0}' in type '{1}'.",
												  name, declType.FullName);
				}
			}

			erred = false;
			foreach (Instruction instr in routedEvtRegInstrs) {
				int[] args = trace.TraceArguments(instr);
				if (args == null) {
					if (!erred)
						logger.LogWarning("Failed to extract routed event name in '{0}'.", method.FullName);
					erred = true;
					continue;
				}
				Instruction ldstr = method.Body.Instructions[args[0]];
				if (ldstr.OpCode.Code != Code.Ldstr) {
					if (!erred)
						logger.LogWarning("Failed to extract routed event name in '{0}'.", method.FullName);
					erred = true;
					continue;
				}

				var name = (string)ldstr.Operand;
				TypeDef declType = method.DeclaringType;

				EventDef eventDef = null;
				if ((eventDef = declType.FindEvent(name)) == null) {
					logger.LogWarning("Failed to find the CLR event of routed event '{0}' in type '{1}'.",
											  name, declType.FullName);
					continue;
				}
				service.SetCanRename(context, eventDef, false);

				if (eventDef.AddMethod != null)
					service.SetCanRename(context, eventDef.AddMethod, false);

				if (eventDef.RemoveMethod != null)
					service.SetCanRename(context, eventDef.RemoveMethod, false);

				if (eventDef.InvokeMethod != null)
					service.SetCanRename(context, eventDef.InvokeMethod, false);

				if (eventDef.HasOtherMethods) {
					foreach (MethodDef accessor in eventDef.OtherMethods)
						service.SetCanRename(context, accessor, false);
				}
			}
		}

		void AnalyzeResources(IConfuserContext context, INameService service, ModuleDefMD module) {
			if (analyzer == null) {
				analyzer = new BAMLAnalyzer(context, service);
				analyzer.AnalyzeElement += AnalyzeBAMLElement;
			}

			var wpfResInfo = new Dictionary<string, Dictionary<string, BamlDocument>>();

			foreach (EmbeddedResource res in module.Resources.OfType<EmbeddedResource>()) {
				Match match = ResourceNamePattern.Match(res.Name);
				if (!match.Success)
					continue;

				var resInfo = new Dictionary<string, BamlDocument>();

				var reader = new ResourceReader(res.CreateReader().AsStream());
				IDictionaryEnumerator enumerator = reader.GetEnumerator();
				while (enumerator.MoveNext()) {
					var name = (string)enumerator.Key;
					if (!name.EndsWith(".baml"))
						continue;

					string typeName;
					byte[] data;
					reader.GetResourceData(name, out typeName, out data);
					BamlDocument document = analyzer.Analyze(module, name, data);
					document.DocumentName = name;
					resInfo.Add(name, document);
				}

				if (resInfo.Count > 0)
					wpfResInfo.Add(res.Name, resInfo);
			}
			if (wpfResInfo.Count > 0)
				context.Annotations.Set(module, BAMLKey, wpfResInfo);
		}
	}
}
