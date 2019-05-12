using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confuser.Core.Project;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Core.Services {
	internal sealed class PackerService : IPackerService {
		private readonly ILoggerFactory loggerFactory;

		public PackerService(IServiceProvider provider) =>
			loggerFactory = provider.GetRequiredService<ILoggerFactory>();

		public void ProtectStub(IConfuserContext context1, string fileName, byte[] module, StrongNameKey snKey,
			IProtection prot, CancellationToken token) {
			var logger = loggerFactory.CreateLogger("packer");
			var context = (ConfuserContext)context1;
			string tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			try {
				string outDir = Path.Combine(tmpDir, Path.GetRandomFileName());
				Directory.CreateDirectory(tmpDir);

				for (int i = 0; i < context.OutputModules.Count; i++) {
					string path = Path.GetFullPath(Path.Combine(tmpDir, context.OutputPaths[i]));
					var dir = Path.GetDirectoryName(path);
					if (!Directory.Exists(dir))
						Directory.CreateDirectory(dir);
					File.WriteAllBytes(path, context.OutputModules[i].ToArray());
				}

				File.WriteAllBytes(Path.Combine(tmpDir, fileName), module);

				var proj = new ConfuserProject {Seed = context.Project.Seed};
				foreach (var rule in context.Project.Rules)
					proj.Rules.Add(rule);
				proj.Add(new ProjectModule {Path = fileName});
				proj.BaseDirectory = tmpDir;
				proj.OutputDirectory = outDir;
				foreach (var path in context.Project.ProbePaths)
					proj.ProbePaths.Add(path);
				proj.ProbePaths.Add(context.Project.BaseDirectory);

				PluginDiscovery discovery = null;
				if (prot != null) {
					var protectionId = prot
						.GetType()
						.GetCustomAttributes(typeof(ExportMetadataAttribute), false)
						.OfType<ExportMetadataAttribute>().Where(a => a.Name == "Id")
						.Single().Value as string;

					var rule = new Rule {Preset = ProtectionPreset.None, Inherit = true, Pattern = "true"};
					rule.Add(new SettingItem<IProtection> {Id = protectionId, Action = SettingItemAction.Add});
					proj.Rules.Add(rule);
					discovery = new PackerDiscovery(protectionId, prot);
				}

				try {
					ConfuserEngine
						.Run(
							new ConfuserParameters {
								ConfigureLogging =
									builder => builder.AddProvider(new PackerLoggerProvider(loggerFactory)),
								PluginDiscovery = discovery,
								Marker = new PackerMarker(snKey),
								Project = proj,
								PackerInitiated = true
							}, token).Wait();
				}
				catch (AggregateException ex) {
					logger.LogCritical("Failed to protect packer stub.");
					throw new ConfuserException(ex);
				}

				context.OutputModules =
					ImmutableArray.Create<Memory<byte>>(File.ReadAllBytes(Path.Combine(outDir, fileName)));
				context.OutputPaths = ImmutableArray.Create(fileName);
			}
			finally {
				try {
					if (Directory.Exists(tmpDir)) {
						Directory.Delete(tmpDir, true);
					}
				}
				catch (IOException ex) {
					logger.LogWarning(ex, "Failed to remove temporary files of packer.");
				}

			}
		}

		private sealed class PackerLoggerProvider : ILoggerProvider {
			private readonly ILoggerFactory baseLoggerFactory;

			public PackerLoggerProvider(ILoggerFactory baseLoggerFactory) => this.baseLoggerFactory = baseLoggerFactory;

			ILogger ILoggerProvider.CreateLogger(string categoryName) =>
				baseLoggerFactory.CreateLogger("packer:" + categoryName);

			void IDisposable.Dispose() {
			}
		}

		private sealed class PackerMarker : Marker {
			readonly StrongNameKey snKey;

			public PackerMarker(StrongNameKey snKey) => this.snKey = snKey;

			protected internal override MarkerResult MarkProject(ConfuserProject proj, ConfuserContext context,
				CancellationToken token) {
				var result = base.MarkProject(proj, context, token);
				foreach (var module in result.Modules)
					context.Annotations.Set(module, SNKey, snKey);
				return result;
			}
		}

		internal class PackerDiscovery : PluginDiscovery {
			private readonly string protectionId;
			private readonly IProtection protection;

			public PackerDiscovery(string protectionId, IProtection protection) {
				this.protectionId = protectionId;
				this.protection = protection;
			}

			protected override AggregateCatalog GetAdditionalPlugIns(ConfuserProject project, ILogger logger) {
				var catalog = base.GetAdditionalPlugIns(project, logger);
				if (protection == null) return catalog;

				return new AggregateCatalog(
					catalog,
					new PackerCompositionCatalog(protectionId, protection));
			}
		}

		private sealed class PackerCompositionCatalog : ComposablePartCatalog {
			private readonly ComposablePartDefinition partDef;

			public PackerCompositionCatalog(string protectionId, IProtection protection) =>
				partDef = new PackerComposablePartDefinition(protectionId, protection);

			public override IEnumerable<Tuple<ComposablePartDefinition, ExportDefinition>> GetExports(
				ImportDefinition definition) {
				if (definition == null) throw new ArgumentNullException(nameof(definition));

				return partDef.ExportDefinitions
					.Where(definition.IsConstraintSatisfiedBy)
					.Select(def => Tuple.Create(partDef, def));
			}
		}

		private sealed class PackerComposablePartDefinition : ComposablePartDefinition {
			private readonly ComposablePart part;

			public PackerComposablePartDefinition(string protectionId, IProtection protection) =>
				part = new PackerComposablePart(protectionId, protection);

			public override IEnumerable<ExportDefinition> ExportDefinitions => part.ExportDefinitions;

			public override IEnumerable<ImportDefinition> ImportDefinitions => part.ImportDefinitions;

			public override ComposablePart CreatePart() => part;
		}

		private sealed class PackerComposablePart : ComposablePart {
			private readonly IProtection prot;
			private readonly ExportDefinition exportDef;

			public PackerComposablePart(string protectionId, IProtection prot) {
				this.prot = prot ?? throw new ArgumentNullException(nameof(prot));
				exportDef = new ExportDefinition(typeof(IProtection).FullName,
					ImmutableDictionary.Create<string, object>()
						.Add("ExportTypeIdentity", typeof(IProtection).FullName)
						.Add(nameof(IProtectionMetadata.Id), protectionId));
			}

			public override IEnumerable<ExportDefinition> ExportDefinitions => ImmutableArray.Create(exportDef);

			public override IEnumerable<ImportDefinition> ImportDefinitions => Enumerable.Empty<ImportDefinition>();

			public override object GetExportedValue(ExportDefinition definition) => prot;

			public override void SetImport(ImportDefinition definition, IEnumerable<Export> exports) {
			}
		}
	}
}
