using System.Linq;
using System.Text.RegularExpressions;
using Confuser.Core;
using Confuser.Renamer.Properties;
using Confuser.Renamer.References;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Confuser.Renamer.Analyzers {
	internal class ResourceAnalyzer : IRenamer {
		static readonly Regex ResourceNamePattern = new Regex("^(.*)\\.resources$");

		public void Analyze(IConfuserContext context, INameService service, IProtectionParameters parameters,
			IDnlibDef def) {
			if (!(def is ModuleDef module)) return;

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(NameProtection._Id);

			string asmName = module.Assembly.Name.String;
			if (!string.IsNullOrEmpty(module.Assembly.Culture) &&
			    asmName.EndsWith(".resources")) {
				// Satellite assembly
				var satellitePattern = new Regex(string.Format("^(.*)\\.{0}\\.resources$", module.Assembly.Culture));
				string nameAsmName = asmName.Substring(0, asmName.Length - ".resources".Length);
				ModuleDef mainModule = context.Modules.SingleOrDefault(mod => mod.Assembly.Name == nameAsmName);
				if (mainModule == null) {
					logger.LogCritical("Could not find main assembly of satellite assembly '{0}'.",
						module.Assembly.FullName);
					throw new ConfuserException();
				}

				string format = "{0}." + module.Assembly.Culture + ".resources";
				foreach (Resource res in module.Resources) {
					Match match = satellitePattern.Match(res.Name);
					if (!match.Success)
						continue;
					string typeName = match.Groups[1].Value;
					TypeDef type = mainModule.FindReflection(typeName);
					if (type == null) {
						logger.LogWarning(Resources.ResourceAnalyzer_Analyze_CouldNotFindResourceType, typeName);
						continue;
					}

					service.ReduceRenameMode(context, type, RenameMode.ASCII);
					service.AddReference(context, type, new ResourceReference(res, type, format));
				}
			}
			else {
				string format = "{0}.resources";
				foreach (Resource res in module.Resources) {
					Match match = ResourceNamePattern.Match(res.Name);
					if (!match.Success || res.ResourceType != ResourceType.Embedded)
						continue;
					string typeName = match.Groups[1].Value;

					if (typeName.EndsWith(".g")) // WPF resources, ignore
						continue;

					// This variable is set true in case the name of the resource doesn't match the name of the class.
					// That happens for the resources in Visual Basic.
					var mismatchingName = false;
					TypeDef type = module.FindReflection(typeName);
					if (type == null) {
						if (typeName.EndsWith(".Resources")) {
							typeName = typeName.Substring(0, typeName.Length - 10) + ".My.Resources.Resources";
							type = module.FindReflection(typeName);
							mismatchingName = type != null;
						}
					}

					if (type == null) {
						logger.LogWarning(Resources.ResourceAnalyzer_Analyze_CouldNotFindResourceType, typeName);
						continue;
					}

					service.ReduceRenameMode(context, type, RenameMode.ASCII);
					service.AddReference(context, type, new ResourceReference(res, type, format));

					if (mismatchingName)
						// Add string type references in case the name didn't match. This will cause the resource to get
						// the same name as the class, despite that not being the case before. But that doesn't really matter.
						FindLdTokenResourceReferences(context, type, match.Groups[1].Value, service);
				}
			}
		}

		private static void FindLdTokenResourceReferences(IConfuserContext context, TypeDef type, string name, INameService service) {
			foreach (var method in type.Methods)
				FindLdTokenResourceReferences(context, type, method, name, service);
		}

		private static void FindLdTokenResourceReferences(IConfuserContext context, TypeDef type, MethodDef method, string name, INameService service) {
			if (!method.HasBody) return;

			foreach (var instr in method.Body.Instructions) {
				if (instr.OpCode.Code == Code.Ldstr && ((string)instr.Operand).Equals(name)) {
					service.AddReference(context, type, new StringTypeReference(instr, type));
				}
			}
		}

		public void PreRename(IConfuserContext context, INameService service, IProtectionParameters parameters,
			IDnlibDef def) {
			//
		}

		public void PostRename(IConfuserContext context, INameService service, IProtectionParameters parameters,
			IDnlibDef def) {
			//
		}
	}
}
