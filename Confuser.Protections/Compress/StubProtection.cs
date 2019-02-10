using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using Confuser.Core;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.MD;
using dnlib.DotNet.Writer;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.Compress {
	[Export(typeof(IProtection)), PartNotDiscoverable]
	[ExportMetadata(nameof(IProtectionMetadata.Id), _FullId)]
	internal sealed class StubProtection : IProtection {
		public const string _FullId = "Ki.Compressor.Protection";

		readonly CompressorContext ctx;
		readonly ModuleDef originModule;

		internal StubProtection(CompressorContext ctx, ModuleDef originModule) {
			this.ctx = ctx;
			this.originModule = originModule;
		}

		public string Name => "Compressor Stub Protection";

		public string Description => "Do some extra works on the protected stub.";

		public ProtectionPreset Preset => ProtectionPreset.None;

		IReadOnlyDictionary<string, IProtectionParameter> IProtection.Parameters => ProtectionParameter.EmptyDictionary;

		void IConfuserComponent.Initialize(IServiceCollection collection) {
			//
		}

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) {
			if (!ctx.CompatMode)
				pipeline.InsertPreStage(PipelineStage.Inspection, new InjPhase(this));
			pipeline.InsertPostStage(PipelineStage.BeginModule, new SigPhase(this));
		}

		private sealed class InjPhase : IProtectionPhase {
			public InjPhase(StubProtection parent) =>
				Parent = parent ?? throw new ArgumentNullException(nameof(parent));

			public StubProtection Parent { get; }

			IConfuserComponent IProtectionPhase.Parent => Parent;

			public ProtectionTargets Targets => ProtectionTargets.Modules;

			public bool ProcessAll => true;

			public string Name => "Module injection";

			void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters,
				CancellationToken token) {
				// Hack the origin module into the assembly to make sure correct type resolution
				var originModule = ((StubProtection)Parent).originModule;
				originModule.Assembly.Modules.Remove(originModule);
				context.Modules[0].Assembly.Modules.Add(((StubProtection)Parent).originModule);
			}
		}

		private sealed class SigPhase : IProtectionPhase {
			public SigPhase(StubProtection parent) =>
				Parent = parent ?? throw new ArgumentNullException(nameof(parent));

			public StubProtection Parent { get; }

			IConfuserComponent IProtectionPhase.Parent => Parent;

			public ProtectionTargets Targets => ProtectionTargets.Modules;

			public bool ProcessAll => false;

			public string Name => "Packer info encoding";

			void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters,
				CancellationToken token) {
				var field = context.CurrentModule.Types[0].FindField("DataField");
				Debug.Assert(field != null);
				context.Registry.GetService<INameService>()?.SetCanRename(context, field, true);

				context.CurrentModuleWriterOptions.WriterEvent += (sender, e) => {
					if (e.Event == ModuleWriterEvent.MDBeginCreateTables) {
						// Add key signature
						var writer = (ModuleWriterBase)sender;
						uint blob = writer.Metadata.BlobHeap.Add(Parent.ctx.KeySig.ToArray());
						uint rid = writer.Metadata.TablesHeap.StandAloneSigTable.Add(new RawStandAloneSigRow(blob));
						Debug.Assert((0x11000000 | rid) == Parent.ctx.KeyToken);

						if (Parent.ctx.CompatMode)
							return;

						// Add File reference
						byte[] hash = SHA1.Create().ComputeHash(Parent.ctx.OriginModule);
						uint hashBlob = writer.Metadata.BlobHeap.Add(hash);

						MDTable<RawFileRow> fileTbl = writer.Metadata.TablesHeap.FileTable;
						uint fileRid = fileTbl.Add(new RawFileRow(
							(uint)FileAttributes.ContainsMetadata,
							writer.Metadata.StringsHeap.Add("koi"),
							hashBlob));
					}
				};
			}
		}
	}
}
