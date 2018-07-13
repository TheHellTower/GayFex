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

				context.CurrentModuleWriterOptions.MetadataOptions.Flags |= MetadataFlags.PreserveStringsOffsets;
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
					var writer = e.Writer;
					ctx.ManifestResources = new List<(uint, uint, UTF8String)>();					

					foreach (var resource in writer.Module.Resources) {
						var rid = writer.Metadata.GetManifestResourceRid(resource);
						if (rid != 0) {
							// The resource has a RID assigned. So it is part of the written module.
							var resourceRow = writer.Metadata.TablesHeap.ManifestResourceTable[rid];
							ctx.ManifestResources.Add((resourceRow.Offset, resourceRow.Flags, resource.Name));
						}
					}

					ctx.EntryPointToken = writer.Metadata.GetToken(ctx.EntryPoint).Raw;
				}
			}
		}
	}
}
