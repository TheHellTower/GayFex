using System.ComponentModel.Composition;
using Confuser.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Optimizations.TailCall {
	[Export(typeof(IProtection))]
	[ExportMetadata(nameof(IProtectionMetadata.Id), FullId)]
	[ExportMetadata(nameof(IProtectionMetadata.MarkerId), Id)]
	public sealed class TailCallProtection : IProtection {
		public const string Id = "tail call";
		public const string FullId = "Cx2.TailCall";

		public ProtectionPreset Preset => ProtectionPreset.None;

		public string Name => "Tail Call Optimization";

		public string Description => "This optimization optimizes methods with tail calls and tail recursions.";

		internal TailCallProtectionParameters Parameters { get; } = new TailCallProtectionParameters();

		public void Initialize(IServiceCollection collection) { }

		public void PopulatePipeline(IProtectionPipeline pipeline) {
			pipeline.InsertPreStage(PipelineStage.OptimizeMethods, new OptimizeRecursionPhase(this));
			pipeline.InsertPreStage(PipelineStage.OptimizeMethods, new AddTailCallPhase(this));
		}
	}
}
