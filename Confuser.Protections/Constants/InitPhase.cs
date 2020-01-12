using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.MD;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.Constants {
	internal sealed class InitPhase : IProtectionPhase {

		public InitPhase(ConstantProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public ConstantProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Modules;

		public bool ProcessAll => false;

		public string Name => "Initialize Constants protection";

		/// <inheritdoc />
		public void Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			var nameService = context.Registry.GetService<INameService>();
			var markerService = context.Registry.GetRequiredService<IMarkerService>();
			var dynCipherService = context.Registry.GetRequiredService<IDynCipherService>();
			var traceService = context.Registry.GetRequiredService<ITraceService>();
			var randomService = context.Registry.GetRequiredService<IRandomService>();

			foreach (var targetModule in parameters.Targets.OfType<ModuleDef>())
			{
				var moduleCtx = new CEContext {
					Protection = Parent,
					Random = randomService.GetRandomGenerator(ConstantProtection._FullId + targetModule.FullName),
					Context = context,
					Module = targetModule,
					Marker = markerService,
					DynCipher = dynCipherService,
					Name = nameService,
					Trace =traceService
				};

				// Extract parameters
				var encryptMode = parameters.GetParameter(context, targetModule, Parent.Parameters.EncryptMode);
				var encodeMode = parameters.GetParameter(context, targetModule, Parent.Parameters.EncodeMode);
				moduleCtx.DecoderCount = parameters.GetParameter(context, targetModule, Parent.Parameters.DecoderCount);

				switch (encryptMode) {
					case Mode.PassThrough:
						moduleCtx.EncryptMode = new PassThroughMode();
						break;
					case Mode.Expression:
						moduleCtx.EncryptMode = new DynamicMode();
						break;
					case Mode.x86:
						moduleCtx.EncryptMode = new x86Mode();
						break;
					default:
						throw new UnreachableException();
				}

				switch (encodeMode) {
					case Mode.PassThrough:
						moduleCtx.EncodeMode = new PassThroughMode();
						break;
					case Mode.Expression:
						moduleCtx.EncodeMode = new DynamicMode();
						break;
					case Mode.x86:
						moduleCtx.EncodeMode = new x86Mode();
						break;
					default:
						throw new UnreachableException();
				}

				context.Annotations.Set(targetModule, ConstantProtection.ContextKey, moduleCtx);
			}
		}
	}
}
