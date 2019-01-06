using System.Collections.Generic;
using System.ComponentModel.Composition;
using Confuser.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	[Export(typeof(IProtection))]
	[ExportMetadata(nameof(IProtectionMetadata.Id), _FullId)]
	[ExportMetadata(nameof(IProtectionMetadata.MarkerId), _Id)]
	[BeforeProtection(ControlFlowProtection._FullId)]
	internal sealed class AntiDebugProtection : IProtection {
		public const string _Id = "anti debug";
		public const string _FullId = "Ki.AntiDebug";

		public string Name => "Anti Debug Protection";

		public string Description => "This protection prevents the assembly from being debugged or profiled.";

		public ProtectionPreset Preset => ProtectionPreset.Minimum;

		IReadOnlyDictionary<string, IProtectionParameter> IProtection.Parameters => Parameters;

		internal AntiDebugProtectionParameters Parameters { get; } = new AntiDebugProtectionParameters();

		void IConfuserComponent.Initialize(IServiceCollection services) => services.AddRuntime();

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) =>
			pipeline.InsertPreStage(PipelineStage.ProcessModule, new AntiDebugProtectionPhase(this));
	}
}
