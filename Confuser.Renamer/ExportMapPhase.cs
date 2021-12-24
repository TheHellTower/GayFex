using System.Collections.Generic;
using System.IO;
using System.Threading;
using Confuser.Core;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Renamer {
	internal class ExportMapPhase : IProtectionPhase {
		public NameProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Modules;

		public string Name => "Export symbol map";

		public bool ProcessAll => true;

		public ExportMapPhase(NameProtection parent) => Parent = parent;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters,
			CancellationToken token) {
			
			var srv = (NameService)context.Registry.GetService<INameService>();
			var map = srv.GetNameMap();
			if (map.Count == 0)
				return;

			string dir = context.OutputDirectory;
			string path = Path.GetFullPath(Path.Combine(dir, CoreConstants.SymbolsFileName));
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			using (var writer = new StreamWriter(File.Create(path))) {
				foreach (var entry in map)
					writer.WriteLine("{0}\t{1}", entry.Key, entry.Value);
			}
		}
	}
}
