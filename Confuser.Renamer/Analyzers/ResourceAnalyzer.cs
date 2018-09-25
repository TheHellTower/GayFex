using System.Linq;
using System.Text.RegularExpressions;
using Confuser.Core;
using Confuser.Renamer.References;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Confuser.Renamer.Analyzers {
	internal class ResourceAnalyzer : IRenamer {
		static readonly Regex ResourceNamePattern = new Regex("^(.*)\\.resources$");

		public void Analyze(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def) {
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
					logger.LogError("Could not find main assembly of satellite assembly '{0}'.", module.Assembly.FullName);
					throw new ConfuserException(null);
				}

				string format = "{0}." + module.Assembly.Culture + ".resources";
				foreach (Resource res in module.Resources) {
					Match match = satellitePattern.Match(res.Name);
					if (!match.Success)
						continue;
					string typeName = match.Groups[1].Value;
					TypeDef type = mainModule.FindReflection(typeName);
					if (type == null) {
						logger.LogWarning("Could not find resource type '{0}'.", typeName);
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

					TypeDef type = module.FindReflection(typeName);
					if (type == null) {
						if (typeName.EndsWith(".Resources")) {
							typeName = typeName.Substring(0, typeName.Length - 10) + ".My.Resources.Resources";
							type = module.FindReflection(typeName);
						}
					}

					if (type == null) {
						logger.LogWarning("Could not find resource type '{0}'.", typeName);
						continue;
					}
					service.ReduceRenameMode(context, type, RenameMode.ASCII);
					service.AddReference(context, type, new ResourceReference(res, type, format));
				}
			}
		}

		public void PreRename(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def) {
			//
		}

		public void PostRename(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def) {
			//
		}
	}
}
