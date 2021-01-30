using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using Confuser.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	[Export(typeof(IProtection))]
	[ExportMetadata(nameof(IProtectionMetadata.Id), FullId)]
	[ExportMetadata(nameof(IProtectionMetadata.MarkerId), Id)]
	internal sealed class HardeningProtection : IProtection {
		public const string Id = "harden";
		public const string FullId = "harden";
		/// <inheritdoc />
		public string Name => "Protection Hardening";

		/// <inheritdoc />
		public string Description => "This component improves the protection code, making it harder to circumvent it.";

		public ProtectionPreset Preset => ProtectionPreset.None;

		IReadOnlyDictionary<string, IProtectionParameter> IProtection.Parameters => ProtectionParameter.EmptyDictionary;

		void IConfuserComponent.Initialize(IServiceCollection collection) {
			//
		}

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) =>
			pipeline.InsertPreStage(PipelineStage.OptimizeMethods, new HardeningProtectionPhase(this));
	}
}
