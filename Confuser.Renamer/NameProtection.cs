using System.Collections.Generic;
using System.ComponentModel.Composition;
using Confuser.Core;
using Confuser.Renamer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Renamer {
	[Export(typeof(IProtection))]
	[ExportMetadata(nameof(IProtectionMetadata.Id), _FullId)]
	[ExportMetadata(nameof(IProtectionMetadata.MarkerId), _Id)]
	[BeforeProtection("constants")]
	internal sealed class NameProtection : IProtection {
		public const string _Id = "rename";
		public const string _FullId = "Ki.Rename";

		public string Name => "Name Protection";

		public string Description =>
			"This protection obfuscate the symbols' name so the decompiled source code can neither be compiled nor read.";

		public ProtectionPreset Preset => ProtectionPreset.Minimum;

		IReadOnlyDictionary<string, IProtectionParameter> IProtection.Parameters => Parameters;

		internal NameProtectionParameters Parameters { get; } = new NameProtectionParameters();

		void IConfuserComponent.Initialize(IServiceCollection collection) =>
			collection.AddSingleton(typeof(INameService), p => new NameService(p, this));

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) {
			pipeline.InsertPostStage(PipelineStage.Inspection, new AnalyzePhase(this));
			pipeline.InsertPostStage(PipelineStage.BeginModule, new RenamePhase(this));
			pipeline.InsertPreStage(PipelineStage.EndModule, new PostRenamePhase(this));
			pipeline.InsertPostStage(PipelineStage.SaveModules, new ExportMapPhase(this));
		}
	}
}
