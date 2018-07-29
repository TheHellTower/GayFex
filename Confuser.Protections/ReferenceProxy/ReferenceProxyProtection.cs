using System;
using System.ComponentModel.Composition;
using Confuser.Core;
using Confuser.Protections.ReferenceProxy;
using Confuser.Protections.Services;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	[Export(typeof(IProtection))]
	[ExportMetadata(nameof(IProtectionMetadata.Id), _FullId)]
	[ExportMetadata(nameof(IProtectionMetadata.MarkerId), _Id)]
	[AfterProtection(AntiDebugProtection._FullId, AntiDumpProtection._FullId)]
	[BeforeProtection(ControlFlowProtection._FullId)]
	internal sealed class ReferenceProxyProtection : IProtection, IReferenceProxyService {
		public const string _Id = "ref proxy";
		public const string _FullId = "Ki.RefProxy";

		internal static object TargetExcluded = new object();
		internal static object Targeted = new object();

		public string Name => "Reference Proxy Protection";

		public string Description => "This protection encodes and hides references to type/method/fields.";

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
