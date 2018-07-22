using Confuser.Core;
using Confuser.Protections.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.TypeScramble {
	internal sealed class TypeScrambleProtection : IProtection {
		public ProtectionPreset Preset => ProtectionPreset.None;

		public string Name => "Type Scrambler";

		public string Description => "Replaces types with generics";

		public string Id => "typescramble";

		public string FullId => "BahNahNah.typescramble";

		void IConfuserComponent.Initialize(IServiceCollection collection) => 
			collection.AddSingleton(typeof(ITypeScrambleService), (p) => new TypeService());

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) {
			pipeline.InsertPreStage(PipelineStage.Inspection, new AnalyzePhase(this));
			pipeline.InsertPostStage(PipelineStage.Inspection, new ScramblePhase(this));
		}
	}
}
