using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Confuser.Core;
using Confuser.Protections.Constants;
using Confuser.Protections.Services;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	[Export(typeof(IProtection))]
	[ExportMetadata(nameof(IProtectionMetadata.Id), _FullId)]
	[ExportMetadata(nameof(IProtectionMetadata.MarkerId), _Id)]
	[BeforeProtection(ControlFlowProtection._FullId)]
	[AfterProtection(ReferenceProxyProtection._FullId)]
	internal sealed class ConstantProtection : IProtection, IConstantService {
		public const string _Id = "constants";
		public const string _FullId = "Ki.Constants";
		internal static readonly object ContextKey = new object();

		public string Name => "Constants Protection";

		public string Description => "This protection encodes and compresses constants in the code.";

		public ProtectionPreset Preset => ProtectionPreset.Normal;

		IReadOnlyDictionary<string, IProtectionParameter> IProtection.Parameters => Parameters;

		internal ConstantProtectionParameters Parameters { get; } = new ConstantProtectionParameters();

		void IConstantService.ExcludeMethod(IConfuserContext context, MethodDef method) => 
			context.GetParameters(method).RemoveParameters(this);

		void IConfuserComponent.Initialize(IServiceCollection collection) => 
			collection.AddSingleton(typeof(IConstantService), this);

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) {
			pipeline.InsertPreStage(PipelineStage.ProcessModule, new InjectPhase(this));
			pipeline.InsertPostStage(PipelineStage.ProcessModule, new EncodePhase(this));
		}
	}
}
