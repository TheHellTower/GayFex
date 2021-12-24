using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Pdb;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Renamer {
	internal sealed class RenamePhase : IProtectionPhase {
		internal RenamePhase(NameProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public NameProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public bool ProcessAll => false;

		public ProtectionTargets Targets => ProtectionTargets.AllDefinitions;

		public string Name => "Renaming";

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters,
			CancellationToken token) {
			var service = (NameService)context.Registry.GetRequiredService<INameService>();
			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(NameProtection._Id);

			logger.LogDebug("Renaming...");
			foreach (var renamer in service.Renamers) {
				foreach (var def in parameters.Targets)
					renamer.PreRename(context, service, parameters, def);

				token.ThrowIfCancellationRequested();
			}

			var targets = service.GetRandom().Shuffle(parameters.Targets);
			var pdbDocs = new HashSet<string>();
			foreach (IDnlibDef def in GetTargetsWithDelay(targets, context, service, logger)/*.WithProgress(logger)*/) {
				if (def is ModuleDef methodDef && parameters.GetParameter(context, def, Parent.Parameters.RickRoll))
					RickRoller.CommenceRickroll(context, methodDef);

				bool canRename = service.CanRename(context, def);
				var mode = service.GetRenameMode(context, def);

				if (def is MethodDef method) {
					if ((canRename || method.IsConstructor) &&
					    parameters.GetParameter(context, method, Parent.Parameters.RenameArguments)) {
						foreach (var param in method.ParamDefs)
							param.Name = null;
					}

					if (parameters.GetParameter(context, method, Parent.Parameters.RenamePdb) && method.HasBody) {
						foreach (var instr in method.Body.Instructions) {
							if (instr.SequencePoint != null && !pdbDocs.Contains(instr.SequencePoint.Document.Url)) {
								instr.SequencePoint.Document.Url = service.ObfuscateName(
									instr.SequencePoint.Document.Url, mode);
								pdbDocs.Add(instr.SequencePoint.Document.Url);
							}
						}

						foreach (var local in method.Body.Variables) {
							if (!string.IsNullOrEmpty(local.Name))
								local.Name = service.ObfuscateName(local.Name, mode);
						}

						if (method.Body.HasPdbMethod)
							method.Body.PdbMethod.Scope = new PdbScope();
					}
				}

				if (!canRename)
					continue;

				service.SetIsRenamed(context, def);

				IList<INameReference> references = service.GetReferences(context, def);
				bool cancel = references.Any(r => r.ShouldCancelRename);
				if (cancel)
					continue;

				if (def is TypeDef typeDef) {
					if (parameters.GetParameter(context, def, Parent.Parameters.FlattenNamespace)) {
						typeDef.Namespace = "";
					}
					else {
						var nsFormat = parameters.GetParameter(context, typeDef, Parent.Parameters.NamespaceFormat);
						typeDef.Namespace = service.ObfuscateName(nsFormat, typeDef.Namespace, mode);
					}
					
					typeDef.Name = service.ObfuscateName(context, typeDef, mode);
					RenameGenericParameters(typeDef.GenericParameters);
				}
				else if (def is MethodDef methodDef2) {
					RenameGenericParameters(methodDef2.GenericParameters);
					def.Name = service.ObfuscateName(context, methodDef2, mode);
				}
				else
					def.Name = service.ObfuscateName(context, def, mode);

				int updatedReferences = -1;
				do {
					var oldUpdatedCount = updatedReferences;
					// This resolves the changed name references and counts how many were changed.
					var updatedReferenceList = references.Where(refer => refer.UpdateNameReference(context, service)).ToArray();
					updatedReferences = updatedReferenceList.Length;
					if (updatedReferences == oldUpdatedCount) {
						var errorBuilder = new StringBuilder();
						errorBuilder.AppendLine("Infinite loop detected while resolving name references.");
						errorBuilder.Append("Processed definition: ").AppendDescription(def, context, service).AppendLine();
						errorBuilder.Append("Assembly: ").AppendLine(context.CurrentModule.FullName);
						errorBuilder.AppendLine("Faulty References:");
						foreach (var reference in updatedReferenceList) {
							errorBuilder.Append(" - ").AppendLine(reference.ToString(context, service));
						}
						logger.LogError(errorBuilder.ToString().Trim());
						throw new ConfuserException();
					}
					token.ThrowIfCancellationRequested();
				} while (updatedReferences > 0);
			}
		}

		private static void RenameGenericParameters(IList<GenericParam> genericParams)
		{
			foreach (var param in genericParams)
				param.Name = ((char) (param.Number + 1)).ToString();
		}

		private static IEnumerable<IDnlibDef> GetTargetsWithDelay(IImmutableList<IDnlibDef> definitions, IConfuserContext context, INameService service, ILogger logger) {
			var delayedItems = ImmutableArray.CreateBuilder<IDnlibDef>();
			var currentList = definitions;
			var lastCount = -1;
			while (currentList.Any()) {
				foreach (var def in currentList) {
					if (service.GetReferences(context, def).Any(r => r.DelayRenaming(context, service, def)))
						delayedItems.Add(def);
					else
						yield return def;
				}

				if (delayedItems.Count == lastCount) {
					var errorBuilder = new StringBuilder();
					errorBuilder.AppendLine("Failed to rename all targeted members, because the references are blocking each other.");
					errorBuilder.AppendLine("Remaining definitions: ");
					foreach (var def in delayedItems) {
						errorBuilder.Append("• ").AppendDescription(def, context, service).AppendLine();
					}
					logger.LogWarning(errorBuilder.ToString().Trim());
					yield break;
				}
				lastCount = delayedItems.Count;
				currentList = delayedItems.ToImmutable();
				delayedItems.Clear();
			}
		}
	}
}
