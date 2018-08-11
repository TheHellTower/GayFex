using System.ComponentModel.Composition;
using Confuser.Core;
using Confuser.Protections.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	[Export(typeof(IProtection))]
	[ExportMetadata(nameof(IProtectionMetadata.Id), _FullId)]
	[ExportMetadata(nameof(IProtectionMetadata.MarkerId), _Id)]
	[BeforeProtection(ControlFlowProtection._FullId)]
	[AfterProtection(ConstantProtection._FullId)]
	internal sealed class ResourceProtection : IProtection {
		public const string _Id = "resources";
		public const string _FullId = "Ki.Resources";

		public string Name => "Resources Protection";

		public string Description => "This protection encodes and compresses the embedded resources.";

		public ProtectionPreset Preset => ProtectionPreset.Normal;

		internal ResourceProtectionParameters Parameters { get; } = new ResourceProtectionParameters();

		void IConfuserComponent.Initialize(IServiceCollection collection) { }

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) => 
			pipeline.InsertPreStage(PipelineStage.ProcessModule, new InjectPhase(this));
	}
}
