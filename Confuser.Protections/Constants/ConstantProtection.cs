using System;
using System.ComponentModel.Composition;
using Confuser.Core;
using Confuser.Protections.Constants;
using Confuser.Protections.Services;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	[Export(typeof(IProtection))]
	[BeforeProtection("Ki.ControlFlow"), AfterProtection("Ki.RefProxy")]
	internal class ConstantProtection : IProtection, IConstantService {
		public const string _Id = "constants";
		public const string _FullId = "Ki.Constants";
		public const string _ServiceId = "Ki.Constants";
		internal static readonly object ContextKey = new object();

		public string Name => "Constants Protection";

		public string Description => "This protection encodes and compresses constants in the code.";

		public string Id => _Id;

		public string FullId => _FullId;

		public ProtectionPreset Preset => ProtectionPreset.Normal;

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
