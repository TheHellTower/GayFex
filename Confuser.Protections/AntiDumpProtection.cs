using System.ComponentModel.Composition;
using Confuser.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	[Export(typeof(IProtection))]
	[ExportMetadata(nameof(IProtectionMetadata.Id), _FullId)]
	[ExportMetadata(nameof(IProtectionMetadata.MarkerId), _Id)]
	[BeforeProtection(ControlFlowProtection._FullId)]
	internal sealed class AntiDumpProtection : IProtection {
		public const string _Id = "anti dump";
		public const string _FullId = "Ki.AntiDump";

		public string Name => "Anti Dump Protection";

		public string Description => "This protection prevents the assembly from being dumped from memory.";

		public ProtectionPreset Preset => ProtectionPreset.Maximum;

		void IConfuserComponent.Initialize(IServiceCollection services) => services.AddRuntime();

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) =>
			pipeline.InsertPreStage(PipelineStage.ProcessModule, new AntiDumpProtectionPhase(this));
	}
}
