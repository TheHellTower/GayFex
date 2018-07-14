using System;
using System.ComponentModel.Composition;
using Confuser.Core;
using Confuser.Protections.ReferenceProxy;
using Confuser.Protections.Services;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	[Export(typeof(IProtection))]
	[AfterProtection("Ki.AntiDebug", "Ki.AntiDump")]
	[BeforeProtection("Ki.ControlFlow")]
	internal class ReferenceProxyProtection : IProtection, IReferenceProxyService {
		public const string _Id = "ref proxy";
		public const string _FullId = "Ki.RefProxy";
		public const string _ServiceId = "Ki.RefProxy";

		internal static object TargetExcluded = new object();
		internal static object Targeted = new object();

		public string Name => "Reference Proxy Protection";

		public string Description => "This protection encodes and hides references to type/method/fields.";

		public string Id => _Id;

		public string FullId => _FullId;

		public ProtectionPreset Preset => ProtectionPreset.Normal;

		void IReferenceProxyService.ExcludeMethod(IConfuserContext context, MethodDef method) => 
			context.GetParameters(method).RemoveParameters(this);

		void IReferenceProxyService.ExcludeTarget(IConfuserContext context, MethodDef method) => 
			context.Annotations.Set(method, TargetExcluded, TargetExcluded);

		bool IReferenceProxyService.IsTargeted(IConfuserContext context, MethodDef method) =>
			context.Annotations.Get<object>(method, Targeted) != null;

		void IConfuserComponent.Initialize(IServiceCollection collection) {
			collection.AddSingleton(typeof(IReferenceProxyService), this);
		}

		void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) => 
			pipeline.InsertPreStage(PipelineStage.ProcessModule, new ReferenceProxyPhase(this));
	}
}
