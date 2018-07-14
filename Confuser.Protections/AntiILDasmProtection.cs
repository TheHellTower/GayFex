using System.ComponentModel.Composition;
using Confuser.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	[Export(typeof(IProtection))]
	internal sealed class AntiILDasmProtection : IProtection {
		public const string _Id = "anti ildasm";
		public const string _FullId = "Ki.AntiILDasm";

		public string Name => "Anti IL Dasm Protection";

		public string Description => "This protection marks the module with a attribute that discourage ILDasm from disassembling it.";

		public string Id => _Id;

		public string FullId => _FullId;

		public ProtectionPreset Preset => ProtectionPreset.Minimum;

		void IConfuserComponent.Initialize(IServiceCollection collection) {
			//
		}

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) => 
			pipeline.InsertPreStage(PipelineStage.ProcessModule, new AntiILDasmProtectionPhase(this));
	}
}
