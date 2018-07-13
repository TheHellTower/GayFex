using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;
using dnlib.DotNet.MD;
using dnlib.DotNet.Writer;

namespace Confuser.Protections.Compress {
	internal class ExtractPhase : ProtectionPhase {
		public ExtractPhase(Compressor parent) : base(parent) { }

		public override ProtectionTargets Targets {
			get { return ProtectionTargets.Modules; }
		}

		public override string Name {
			get { return "Packer info extraction"; }
		}

		protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
			if (context.Packer == null)
				return;

			bool isExe = context.CurrentModule.Kind == ModuleKind.Windows ||
						 context.CurrentModule.Kind == ModuleKind.Console;

			if (context.Annotations.Get<CompressorContext>(context, Compressor.ContextKey) != null) {
				if (isExe) {
					context.Logger.Error("Too many executable modules!");
					throw new ConfuserException(null);
				}
				return;
			}

			if (isExe) {
				var ctx = new CompressorContext {
					ModuleIndex = context.CurrentModuleIndex,
					Assembly = context.CurrentModule.Assembly,
					CompatMode = parameters.GetParameter(context, null, "compat", false)
				};
				context.Annotations.Set(context, Compressor.ContextKey, ctx);

				ctx.ModuleName = context.CurrentModule.Name;
				ctx.EntryPoint = context.CurrentModule.EntryPoint;
				ctx.Kind = context.CurrentModule.Kind;

				if (!ctx.CompatMode) {
					context.CurrentModule.Name = "koi";
					context.CurrentModule.EntryPoint = null;
					context.CurrentModule.Kind = ModuleKind.NetModule;
				}

				context.CurrentModuleWriterOptions.WriterEvent += new ResourceRecorder(ctx, context.CurrentModule).WriterEvent;
			}
		}

		class ResourceRecorder {
			readonly CompressorContext ctx;
			ModuleDef targetModule;

			public ResourceRecorder(CompressorContext ctx, ModuleDef module) {
				this.ctx = ctx;
				targetModule = module;
			}

			public void WriterEvent(object sender, ModuleWriterEventArgs e) {
				if (e.Event == ModuleWriterEvent.MDEndAddResources) {
					var writer = (ModuleWriterBase)sender;
					ctx.ManifestResources = new List<Tuple<uint, uint, string>>();
					Dictionary<uint, byte[]> stringDict = writer.Metadata.StringsHeap.GetAllRawData().ToDictionary(pair => pair.Key, pair => pair.Value);
					MDTable<RawManifestResourceRow> manifestResourceTable = writer.Metadata.TablesHeap.ManifestResourceTable;
					for (uint i = 1; i <= manifestResourceTable.Rows; i++) {
						RawManifestResourceRow resource = manifestResourceTable[i];
						ctx.ManifestResources.Add(Tuple.Create(resource.Offset, resource.Flags, Encoding.UTF8.GetString(stringDict[resource.Name])));
					}
					ctx.EntryPointToken = writer.Metadata.GetToken(ctx.EntryPoint).Raw;
				}
			}
		}
	}
}
