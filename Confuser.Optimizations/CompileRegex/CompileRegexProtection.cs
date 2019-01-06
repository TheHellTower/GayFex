using System.Collections.Generic;
using System.ComponentModel.Composition;
using Confuser.Core;
using Confuser.Optimizations.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Optimizations.CompileRegex {
	[Export(typeof(IProtection))]
	[ExportMetadata(nameof(IProtectionMetadata.Id), FullId)]
	[ExportMetadata(nameof(IProtectionMetadata.MarkerId), Id)]
	[BeforeProtection("Ki.ControlFlow", "Ki.Resources")]
	public sealed class CompileRegexProtection : IProtection {
		public const string Id = "compile regex";
		public const string FullId = "Cx2.CompileRegex";

		internal const string _RegexNamespace = "System.Text.RegularExpressions";
		internal const string _RegexTypeFullName = _RegexNamespace + ".Regex";

		public ProtectionPreset Preset => ProtectionPreset.None;

		public string Name => "Compile Regular Expressions";

		public string Description => "This optimization will search uses of regular expressions and create the compiled code for the expression if possible.";

		IReadOnlyDictionary<string, IProtectionParameter> IProtection.Parameters => Parameters;

		internal CompileRegexProtectionParameters Parameters { get; } = new CompileRegexProtectionParameters();

		public void Initialize(IServiceCollection collection) =>
			collection.AddSingleton(typeof(ICompileRegexService), p => new CompileRegexService());

		public void PopulatePipeline(IProtectionPipeline pipeline) {
			pipeline.InsertPostStage(PipelineStage.Inspection, new InspectPhase(this));
			pipeline.InsertPreStage(PipelineStage.ProcessModule, new ExtractPhase(this));
			pipeline.InsertPostStage(PipelineStage.ProcessModule, new CompilePhase(this));
			pipeline.InsertPostStage(PipelineStage.ProcessModule, new InjectPhase(this));
		}
	}
}
