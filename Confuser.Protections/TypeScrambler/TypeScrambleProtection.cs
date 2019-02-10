using System.Collections.Generic;
using System.ComponentModel.Composition;
using Confuser.Core;
using Confuser.Protections.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.TypeScramble {
	[Export(typeof(IProtection))]
	[ExportMetadata(nameof(IProtectionMetadata.Id), _FullId)]
	[ExportMetadata(nameof(IProtectionMetadata.MarkerId), _Id)]
	internal sealed class TypeScrambleProtection : IProtection {
		public const string _Id = "typescramble";
		public const string _FullId = "BahNahNah.typescramble";

		public ProtectionPreset Preset => ProtectionPreset.None;

		public string Name => "Type Scrambler";

		public string Description => "Replaces types with generics";

		IReadOnlyDictionary<string, IProtectionParameter> IProtection.Parameters => ProtectionParameter.EmptyDictionary;

		void IConfuserComponent.Initialize(IServiceCollection collection) =>
			collection.AddSingleton(typeof(ITypeScrambleService), (p) => new TypeService());

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) {
			pipeline.InsertPreStage(PipelineStage.Inspection, new AnalyzePhase(this));
			pipeline.InsertPostStage(PipelineStage.Inspection, new ScramblePhase(this));
		}
	}
}
