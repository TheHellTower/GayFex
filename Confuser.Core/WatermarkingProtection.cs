using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Confuser.Core {
	[Export(typeof(IProtection))]
	[ExportMetadata(nameof(IProtectionMetadata.Id), FullId)]
	[ExportMetadata(nameof(IProtectionMetadata.MarkerId), Id)]
	public sealed class WatermarkingProtection : IProtection {
		private const string Id = "watermark";
		private const string FullId = "Cx.Watermark";

		/// <inheritdoc />
		public string Name => "Watermarking";

		/// <inheritdoc />
		public string Description =>
			"This applies a watermark to the assembly, showing that ConfuserEx protected the assembly. So people try to reverse the obfuscation know to just give up.";

		/// <inheritdoc />
		public void Initialize(IServiceCollection collection) { }

		/// <inheritdoc />
		public void PopulatePipeline(IProtectionPipeline pipeline) =>
			pipeline.InsertPreStage(PipelineStage.BeginModule, new WatermarkingPhase(this));

		/// <inheritdoc />
		public ProtectionPreset Preset => ProtectionPreset.None;

		/// <inheritdoc />
		public IReadOnlyDictionary<string, IProtectionParameter> Parameters { get; }

		private sealed class WatermarkingPhase : IProtectionPhase {
			/// <inheritdoc />
			public WatermarkingPhase(WatermarkingProtection parent) => 
				Parent = parent ?? throw new ArgumentNullException(nameof(parent));

			/// <inheritdoc />
			public IConfuserComponent Parent { get; }

			/// <inheritdoc />
			public ProtectionTargets Targets => ProtectionTargets.Modules;

			/// <inheritdoc />
			public string Name => "Apply watermark";

			/// <inheritdoc />
			public bool ProcessAll => false;

			/// <inheritdoc />
			public void Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
				var marker = context.Registry.GetService<IMarkerService>();
				var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("watermark");

				logger.LogDebug("Watermarking...");
				foreach (var module in parameters.Targets.OfType<ModuleDef>()) {
					var attrRef = module.CorLibTypes.GetTypeRef("System", "Attribute");
					var attrType = module.FindNormal("ConfusedByAttribute");
					if (attrType == null) {
						attrType = new TypeDefUser("", "ConfusedByAttribute", attrRef);
						module.Types.Add(attrType);
						marker.Mark(context, attrType, null);
					}

					var ctor = attrType.FindInstanceConstructors()
						.FirstOrDefault(m => m.Parameters.Count == 1 && m.Parameters[0].Type == module.CorLibTypes.String);
					if (ctor == null) {
						ctor = new MethodDefUser(
							".ctor",
							MethodSig.CreateInstance(module.CorLibTypes.Void, module.CorLibTypes.String),
							MethodImplAttributes.Managed,
							MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName) {
							Body = new CilBody {MaxStack = 1}
						};
						ctor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
						ctor.Body.Instructions.Add(OpCodes.Call.ToInstruction(new MemberRefUser(module, ".ctor",
							MethodSig.CreateInstance(module.CorLibTypes.Void), attrRef)));
						ctor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
						attrType.Methods.Add(ctor);
						marker.Mark(context, ctor, null);
					}

					var attr = new CustomAttribute(ctor);
					attr.ConstructorArguments.Add(new CAArgument(module.CorLibTypes.String, ConfuserEngine.Version));

					module.CustomAttributes.Add(attr);
				}
			}
		}
	}
}
