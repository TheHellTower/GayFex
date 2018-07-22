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
		public ITaskItem[] ReferenceAssemblies { get; set; }

		[Required]
		public ITaskItem SourceAssembly { get; set; }

		public ITaskItem AssemblyKeyFile { get; set; }

		[Required]
		public ITaskItem OutputAssembly { get; set; }

		[Required]
		public ITaskItem IntermediateOutputPath { get; set; }

		[Required, Output]
		public ITaskItem ResultProject { get; set; }

		public override bool Execute() {
			var project = new ConfuserProject();
			if (SourceProject != null) {
				var xmlDoc = new XmlDocument();
				xmlDoc.Load(SourceProject.ItemSpec);

				// Probe Paths are not required, because all dependent assemblies are added as external modules.
				project.ProbePaths.Clear();
			}

			project.BaseDirectory = Path.GetDirectoryName(SourceAssembly.ItemSpec);
			if (!project.Any(m => m.Path.Equals(Path.GetFileName(SourceAssembly.ItemSpec)))) {
				project.Add(new ProjectModule() {
					Path = SourceAssembly.ItemSpec,
					SNKeyPath = AssemblyKeyFile.ItemSpec,
					IsExternal = false
				});
			}

			foreach (var refAssembly in ReferenceAssemblies) {
				project.Add(new ProjectModule() {
					Path = refAssembly.ItemSpec,
					IsExternal = true
				});
			}

			project.Save().Save(ResultProject.ItemSpec);

			return true;
		}
	}
}
