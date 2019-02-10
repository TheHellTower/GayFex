using System;
using System.Threading;
using Confuser.Core;
using Confuser.Renamer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Renamer {
	internal class PostRenamePhase : IProtectionPhase {
		public PostRenamePhase(NameProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public NameProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public bool ProcessAll => true;

		public ProtectionTargets Targets => ProtectionTargets.AllDefinitions;

		public string Name => "Post-renaming";

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters,
			CancellationToken token) {
			var service = (NameService)context.Registry.GetRequiredService<INameService>();

			foreach (var renamer in service.Renamers) {
				foreach (var def in parameters.Targets)
					renamer.PostRename(context, service, parameters, def);
				token.ThrowIfCancellationRequested();
			}
		}
	}
}
