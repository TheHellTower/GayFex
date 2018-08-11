using System.ComponentModel.Composition;
using Confuser.Core;
using Confuser.Protections.AntiTamper;
using Confuser.Protections.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	[Export(typeof(IProtection))]
	[ExportMetadata(nameof(IProtectionMetadata.Id), _FullId)]
	[ExportMetadata(nameof(IProtectionMetadata.MarkerId), _Id)]
	[BeforeProtection(ControlFlowProtection._FullId)]
	[AfterProtection(ConstantProtection._FullId)]
	internal sealed class AntiTamperProtection : IProtection {
		public const string _Id = "anti tamper";
		public const string _FullId = "Ki.AntiTamper";
		internal static readonly object HandlerKey = new object();

		public string Name => "Anti Tamper Protection";

		public string Description => "This protection ensures the integrity of application.";

		public ProtectionPreset Preset => ProtectionPreset.Maximum;

		internal AntiTamperProtectionParameters Parameters { get; } = new AntiTamperProtectionParameters();

		void IConfuserComponent.Initialize(IServiceCollection collection) => 
			collection.AddTransient(typeof(IAntiTamperService), (p) => new AntiTamperService(this));

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) {
			pipeline.InsertPreStage(PipelineStage.OptimizeMethods, new InjectPhase(this));
			pipeline.InsertPreStage(PipelineStage.EndModule, new MetadataPhase(this));
		}
	}
}
