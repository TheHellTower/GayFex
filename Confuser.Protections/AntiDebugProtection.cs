using System.ComponentModel.Composition;
using Confuser.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	[BeforeProtection("Ki.ControlFlow")]
	[Export(typeof(IProtection))]
	internal sealed class AntiDebugProtection : IProtection {
		public const string _Id = "anti debug";
		public const string _FullId = "Ki.AntiDebug";

		public string Name => "Anti Debug Protection";

		public string Description => "This protection prevents the assembly from being debugged or profiled.";

		public string Id => _Id;

		public string FullId => _FullId;

		public ProtectionPreset Preset => ProtectionPreset.Minimum;

		void IConfuserComponent.Initialize(IServiceCollection context) {
			//
		}

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) => 
			pipeline.InsertPreStage(PipelineStage.ProcessModule, new AntiDebugProtectionPhase(this));
	}
}
