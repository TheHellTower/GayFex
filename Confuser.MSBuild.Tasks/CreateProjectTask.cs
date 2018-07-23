using System.IO;
using System.Linq;
using System.Xml;
using Confuser.Core.Project;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Confuser.MSBuild.Tasks {
	public sealed class CreateProjectTask : Task {
		public ITaskItem SourceProject { get; set; }

		[Required]
		public ITaskItem[] References { get; set; }

		[Required]
		public ITaskItem AssemblyPath { get; set; }

		public ITaskItem KeyFilePath { get; set; }

		[Required, Output]
		public ITaskItem ResultProject { get; set; }

		public override bool Execute() {
			var project = new ConfuserProject();
			if (!string.IsNullOrWhiteSpace(SourceProject?.ItemSpec)) {
				var xmlDoc = new XmlDocument();
				xmlDoc.Load(SourceProject.ItemSpec);
				project.Load(xmlDoc);

				// Probe Paths are not required, because all dependent assemblies are added as external modules.
				project.ProbePaths.Clear();
			}

			project.BaseDirectory = Path.GetDirectoryName(AssemblyPath.ItemSpec);
			var mainModule = GetOrCreateProjectModule(project, AssemblyPath.ItemSpec);
			if (!string.IsNullOrWhiteSpace(KeyFilePath?.ItemSpec)) {
				mainModule.SNKeyPath = KeyFilePath.ItemSpec;
			}

			foreach (var probePath in References.Select(r => Path.GetDirectoryName(r.ItemSpec)).Distinct()) {
				project.ProbePaths.Add(probePath);
			}

			project.Save().Save(ResultProject.ItemSpec);

			return true;
		}

		private static ProjectModule GetOrCreateProjectModule(ConfuserProject project, string assemblyPath, bool isExternal = false) {
			var assemblyFileName = Path.GetFileName(assemblyPath);
			var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
			foreach (var module in project) {
				if (string.Equals(module.Path, assemblyFileName) || string.Equals(module.Path, assemblyName)) {
					return module;
				}
			}
			var result = new ProjectModule {
				Path = assemblyPath,
				IsExternal = isExternal
			};
			project.Add(result);
			return result;
		}
	}
}
