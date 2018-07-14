using System.ComponentModel.Composition;
using Confuser.Core;
using Confuser.Protections.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	[Export(typeof(IProtection))]
	[BeforeProtection("Ki.ControlFlow"), AfterProtection("Ki.Constants")]
	internal sealed class ResourceProtection : IProtection {
		public const string _Id = "resources";
		public const string _FullId = "Ki.Resources";
		public const string _ServiceId = "Ki.Resources";

		public string Name => "Resources Protection";

		public string Description => "This protection encodes and compresses the embedded resources.";

		public string Id => _Id;

		public string FullId => _FullId;

		public ProtectionPreset Preset => ProtectionPreset.Normal;

		void IConfuserComponent.Initialize(IServiceCollection collection) { }

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) => 
			pipeline.InsertPreStage(PipelineStage.ProcessModule, new InjectPhase(this));
	}
}
