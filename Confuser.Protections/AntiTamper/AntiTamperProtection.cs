using System.ComponentModel.Composition;
using Confuser.Core;
using Confuser.Protections.AntiTamper;
using Confuser.Protections.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	[Export(typeof(IProtection))]
	[BeforeProtection("Ki.ControlFlow"), AfterProtection("Ki.Constants")]
	internal class AntiTamperProtection : IProtection {
		public const string _Id = "anti tamper";
		public const string _FullId = "Ki.AntiTamper";
		public const string _ServiceId = "Ki.AntiTamper";
		internal static readonly object HandlerKey = new object();

		public string Name => "Anti Tamper Protection";

		public string Description => "This protection ensures the integrity of application.";

		public string Id => _Id;

		public string FullId => _FullId;

		public ProtectionPreset Preset => ProtectionPreset.Maximum;

		void IConfuserComponent.Initialize(IServiceCollection collection) => 
			collection.AddTransient(typeof(IAntiTamperService), (p) => new AntiTamperService(this));

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) {
			pipeline.InsertPreStage(PipelineStage.OptimizeMethods, new InjectPhase(this));
			pipeline.InsertPreStage(PipelineStage.EndModule, new MetadataPhase(this));
		}
	}
}
