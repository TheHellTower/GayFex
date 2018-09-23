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

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			var srv = (NameService)context.Registry.GetRequiredService<INameService>();

			var usedFiles = new HashSet<string>();

			foreach (var def in parameters.Targets) {
				var module = (def as ModuleDef) ?? ((IOwnerModule)def).Module;

				var map = srv.GetNameMap(module);
				if (map.Count == 0)
					return;

				var fileName = parameters.GetParameter(context, module, Parent.Parameters.SymbolMapFileName);
				string path = Path.GetFullPath(Path.Combine(context.OutputDirectory, fileName));
				string dir = Path.GetDirectoryName(path);
				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir);

				var fileMode = FileMode.CreateNew;
				if (File.Exists(path)) {
					if (usedFiles.Add(path))
						fileMode = FileMode.Truncate;
					else
						fileMode = FileMode.Append;
				} else
					usedFiles.Add(path);

				using (var writer = new StreamWriter(new FileStream(path, fileMode, FileAccess.Write, FileShare.None, 4096))) {
					foreach (var entry in map)
						writer.WriteLine("{0}\t{1}", entry.Key, entry.Value);
				}
			}

		}
	}
}
