using System.Collections.Generic;
using System.ComponentModel.Composition;
using Confuser.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	[Export(typeof(IProtection))]
	[ExportMetadata(nameof(IProtectionMetadata.Id), _FullId)]
	[ExportMetadata(nameof(IProtectionMetadata.MarkerId), _Id)]
	internal sealed class AntiILDasmProtection : IProtection {
		public const string _Id = "anti ildasm";
		public const string _FullId = "Ki.AntiILDasm";

		public string Name => "Anti IL Dasm Protection";

		public string Description => "This protection marks the module with a attribute that discourage ILDasm from disassembling it.";

		public ProtectionPreset Preset => ProtectionPreset.Minimum;

		IReadOnlyDictionary<string, IProtectionParameter> IProtection.Parameters => ProtectionParameter.EmptyDictionary;

		void IConfuserComponent.Initialize(IServiceCollection collection) {
			//
		}

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) => 
			pipeline.InsertPreStage(PipelineStage.ProcessModule, new AntiILDasmProtectionPhase(this));
	}
}
