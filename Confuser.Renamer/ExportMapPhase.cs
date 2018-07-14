using System.IO;
using System.Threading;
using Confuser.Core;
using Confuser.Renamer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Renamer {
	internal class ExportMapPhase : IProtectionPhase {
		public IConfuserComponent Parent { get; private set; }

		public ProtectionTargets Targets => ProtectionTargets.Modules;

		public string Name => "Export symbol map";

		public bool ProcessAll => true;

		public ExportMapPhase(NameProtection parent) => Parent = parent;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			var srv = context.Registry.GetRequiredService<INameService>();
			var map = srv.GetNameMap();
			if (map.Count == 0)
				return;

			string path = Path.GetFullPath(Path.Combine(context.OutputDirectory, "symbols.map"));
			string dir = Path.GetDirectoryName(path);
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			using (var writer = new StreamWriter(File.OpenWrite(path))) {
				foreach (var entry in map)
					writer.WriteLine("{0}\t{1}", entry.Key, entry.Value);
			}
		}
	}
}
