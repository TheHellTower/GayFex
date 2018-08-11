using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Pdb;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Renamer {
	internal sealed class RenamePhase : IProtectionPhase {
		internal RenamePhase(NameProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public NameProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public bool ProcessAll => false;

		public ProtectionTargets Targets => ProtectionTargets.AllDefinitions;

		public string Name => "Renaming";

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			var service = (NameService)context.Registry.GetRequiredService<INameService>();
			var logger = context.Registry.GetRequiredService<ILoggingService>().GetLogger("naming");

			logger.Debug("Renaming...");
			foreach (var renamer in service.Renamers) {
				foreach (var def in parameters.Targets)
					renamer.PreRename(context, service, parameters, def);

				token.ThrowIfCancellationRequested();
			}

			var targets = service.GetRandom().Shuffle(parameters.Targets);
			var pdbDocs = new HashSet<string>();
			foreach (var def in targets.WithProgress(logger)) {
				if (def is ModuleDef && parameters.GetParameter(context, def, "rickroll", false))
					RickRoller.CommenceRickroll(context, (ModuleDef)def);

				bool canRename = service.CanRename(context, def);
				var mode = service.GetRenameMode(context, def);

				if (def is MethodDef method) {
					if ((canRename || method.IsConstructor) && parameters.GetParameter(context, def, "renameArgs", true)) {
						foreach (var param in method.ParamDefs)
							param.Name = null;
					}

					if (parameters.GetParameter(context, def, "renPdb", false) && method.HasBody) {
						foreach (var instr in method.Body.Instructions) {
							if (instr.SequencePoint != null && !pdbDocs.Contains(instr.SequencePoint.Document.Url)) {
								instr.SequencePoint.Document.Url = service.ObfuscateName(instr.SequencePoint.Document.Url, mode);
								pdbDocs.Add(instr.SequencePoint.Document.Url);
							}
						}
						foreach (var local in method.Body.Variables) {
							if (!string.IsNullOrEmpty(local.Name))
								local.Name = service.ObfuscateName(local.Name, mode);
						}
						method.Body.PdbMethod.Scope = new PdbScope();
					}
				}

				if (!canRename)
					continue;

				var references = service.GetReferences(context, def);
				bool cancel = false;
				foreach (var refer in references) {
					cancel |= refer.ShouldCancelRename();
					if (cancel) break;
				}
				if (cancel)
					continue;

				if (def is TypeDef typeDef) {
					if (parameters.GetParameter(context, def, "flatten", true)) {
						typeDef.Name = service.ObfuscateName(typeDef.FullName, mode);
						typeDef.Namespace = "";
					}
					else {
						typeDef.Namespace = service.ObfuscateName(typeDef.Namespace, mode);
						typeDef.Name = service.ObfuscateName(typeDef.Name, mode);
					}
					foreach (var param in typeDef.GenericParameters)
						param.Name = ((char)(param.Number + 1)).ToString();
				}
				else if (def is MethodDef) {
					foreach (var param in ((MethodDef)def).GenericParameters)
						param.Name = ((char)(param.Number + 1)).ToString();

					def.Name = service.ObfuscateName(def.Name, mode);
				}
				else
					def.Name = service.ObfuscateName(def.Name, mode);

				foreach (var refer in references.ToList()) {
					if (!refer.UpdateNameReference(context, service)) {
						logger.ErrorFormat("Failed to update name reference on '{0}'.", def);
						throw new ConfuserException(null);
					}
				}
				token.ThrowIfCancellationRequested();
			}
		}
	}
}
