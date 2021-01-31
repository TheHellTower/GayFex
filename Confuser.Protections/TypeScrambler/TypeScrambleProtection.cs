using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Confuser.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.TypeScrambler {
	[Export(typeof(IProtection))]
	[ExportMetadata(nameof(IProtectionMetadata.Id), FullId)]
	[ExportMetadata(nameof(IProtectionMetadata.MarkerId), Id)]
	internal sealed class TypeScrambleProtection : IProtection {
		public const string Id = "typescramble";
		public const string FullId = "BahNahNah.typescramble";
		public string Name => "Type Scrambler";

		public string Description => "Replaces types with generics";

		public ProtectionPreset Preset => ProtectionPreset.None;

		IReadOnlyDictionary<string, IProtectionParameter> IProtection.Parameters => Parameters;

		internal TypeScrambleProtectionParameters Parameters { get; } = new TypeScrambleProtectionParameters();
		
		void IConfuserComponent.Initialize(IServiceCollection collection) {
			if (collection == null) throw new ArgumentNullException(nameof(collection));

			collection.AddSingleton(new TypeService());
		}

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) {
			if (pipeline == null) throw new ArgumentNullException(nameof(pipeline));

			pipeline.InsertPreStage(PipelineStage.Inspection, new AnalyzePhase(this));
			pipeline.InsertPostStage(PipelineStage.ProcessModule, new ScramblePhase(this));
		}
	}
}
