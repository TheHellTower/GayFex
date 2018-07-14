using System.ComponentModel.Composition;
using Confuser.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	[BeforeProtection("Ki.ControlFlow")]
	[Export(typeof(IProtection))]
	internal class AntiDumpProtection : IProtection {
		public const string _Id = "anti dump";
		public const string _FullId = "Ki.AntiDump";

		public string Name => "Anti Dump Protection";

		public string Description => "This protection prevents the assembly from being dumped from memory.";

		public string Id => _Id;

		public string FullId => _FullId;

		public ProtectionPreset Preset => ProtectionPreset.Maximum;

		void IConfuserComponent.Initialize(IServiceCollection collection) {
			//
		}

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) => 
			pipeline.InsertPreStage(PipelineStage.ProcessModule, new AntiDumpProtectionPhase(this));
	}
}
