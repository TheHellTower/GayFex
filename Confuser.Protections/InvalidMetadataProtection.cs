using System.Collections.Generic;
using System.ComponentModel.Composition;
using Confuser.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	[Export(typeof(IProtection))]
	[ExportMetadata(nameof(IProtectionMetadata.Id), _FullId)]
	[ExportMetadata(nameof(IProtectionMetadata.MarkerId), _Id)]
	internal sealed class InvalidMetadataProtection : IProtection {
		public const string _Id = "invalid metadata";
		public const string _FullId = "Ki.InvalidMD";

		public string Name => "Invalid Metadata Protection";

		public string Description =>
			"This protection adds invalid metadata to modules to prevent disassembler/decompiler from opening them.";

		public string Id => _Id;

		public string FullId => _FullId;

		public ProtectionPreset Preset => ProtectionPreset.None;

		IReadOnlyDictionary<string, IProtectionParameter> IProtection.Parameters => ProtectionParameter.EmptyDictionary;

		void IConfuserComponent.Initialize(IServiceCollection collection) {
			//
		}

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) =>
			pipeline.InsertPostStage(PipelineStage.BeginModule, new InvalidMetadataProtectionPhase(this));
	}
}
