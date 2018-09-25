using System.IO;
using System.Xml;
using Confuser.Core;
using Confuser.Core.Project;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.Logging;

namespace Confuser.MSBuild.Tasks {
	public sealed class ConfuseTask : Task {
		[Required]
		public ITaskItem Project { get; set; }

		[Required, Output]
		public ITaskItem OutputAssembly { get; set; }

		public override bool Execute() {
			var project = new ConfuserProject();
			var xmlDoc = new XmlDocument();
			xmlDoc.Load(Project.ItemSpec);
			project.Load(xmlDoc);
			project.OutputDirectory = Path.GetDirectoryName(OutputAssembly.ItemSpec);

			var logger = new MSBuildLogger(Log);
			var parameters = new ConfuserParameters {
				Project = project,
				ConfigureLogging = builder => builder.AddProvider(logger)
			};

			return ConfuserEngine.Run(parameters).Result;
		}
	}
}
