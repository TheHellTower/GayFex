using System.ComponentModel.Composition;
using Confuser.Core;
using Confuser.Protections.ControlFlow;
using Confuser.Protections.Services;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	[Export(typeof(IProtection))]
	internal sealed class ControlFlowProtection : IProtection, IControlFlowService {
		public const string _Id = "ctrl flow";
		public const string _FullId = "Ki.ControlFlow";
		public const string _ServiceId = "Ki.ControlFlow";

		public string Name => "Control Flow Protection";

		public string Description => "This protection mangles the code in the methods so that decompilers cannot decompile the methods.";

		public string Id => _Id;

		public string FullId => _FullId;

		public ProtectionPreset Preset => ProtectionPreset.Normal;

		void IControlFlowService.ExcludeMethod(IConfuserContext context, MethodDef method) => 
			context.GetParameters(method).RemoveParameters(this);

		void IConfuserComponent.Initialize(IServiceCollection collection) => 
			collection.AddSingleton(typeof(IControlFlowService), this);

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) => 
			pipeline.InsertPreStage(PipelineStage.OptimizeMethods, new ControlFlowPhase(this));
	}
}
