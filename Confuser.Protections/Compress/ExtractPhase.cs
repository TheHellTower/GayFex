using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.Compress {
	internal sealed class ExtractPhase : IProtectionPhase {
		public ExtractPhase(Compressor parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public Compressor Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Modules;

		public string Name => "Packer info extraction";

		public bool ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			if (context.Packer == null)
				return;

			var logger = context.Registry.GetRequiredService<ILoggingService>().GetLogger("compressor");

			bool isExe = context.CurrentModule.Kind == ModuleKind.Windows ||
						 context.CurrentModule.Kind == ModuleKind.Console;

			if (context.Annotations.Get<CompressorContext>(context, Compressor.ContextKey) != null) {
				if (isExe) {
					logger.Error("Too many executable modules!");
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

				context.CurrentModuleWriterOptions.WriterEvent += new ResourceRecorder(ctx).WriterEvent;
			}
		}

		private sealed class ResourceRecorder {
			private readonly CompressorContext ctx;

			public ResourceRecorder(CompressorContext ctx) => this.ctx = ctx;

			public void WriterEvent(object sender, ModuleWriterEventArgs e) {
				if (e.Event == ModuleWriterEvent.MDEndAddResources) {
					var writer = e.Writer;
					ctx.ManifestResources = new List<(uint, uint, UTF8String)>();

					foreach (var resource in writer.Module.Resources) {
						var rid = writer.Metadata.GetManifestResourceRid(resource);
						if (rid != 0) {
							// The resource has a RID assigned. So it is part of the written module.
							var resourceRow = writer.Metadata.TablesHeap.ManifestResourceTable[rid];
							Debug.Assert(resourceRow.Name == writer.Metadata.StringsHeap.Add(resource.Name),
								"Resource with RID has different name in StringHeap?!");
							ctx.ManifestResources.Add((resourceRow.Offset, resourceRow.Flags, resource.Name));
						}
					}

					ctx.EntryPointToken = writer.Metadata.GetToken(ctx.EntryPoint).Raw;
				}
			}
		}
	}
}
