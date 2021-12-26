using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.Helpers;
using Confuser.Protections.Services;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Protections.Resources {
	internal sealed class InjectPhase : IProtectionPhase {
		public InjectPhase(ResourceProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public ResourceProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Methods;

		public string Name => "Resource encryption helpers injection";

		public bool ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters,
			CancellationToken token) {
			if (parameters.Targets.Any()) {
				var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(ResourceProtection._Id);
				if (!UTF8String.IsNullOrEmpty(context.CurrentModule.Assembly.Culture)) {
					logger.LogDebug("Skipping resource encryption for satellite assembly '{0}'.",
						context.CurrentModule.Assembly.FullName);
					return;
				}

				var name = context.Registry.GetService<INameService>();
				var marker = context.Registry.GetRequiredService<IMarkerService>();
				var moduleCtx = new REContext {
					Random = context.Registry.GetRequiredService<IRandomService>()
						.GetRandomGenerator(ResourceProtection._FullId),
					Context = context,
					Module = context.CurrentModule,
					Marker = marker,
					DynCipher = context.Registry.GetRequiredService<IDynCipherService>(),
					Name = name,
					Trace = context.Registry.GetRequiredService<ITraceService>()
				};

				// Extract parameters
				moduleCtx.Mode = parameters.GetParameter(context, context.CurrentModule, Parent.Parameters.Mode);

				switch (moduleCtx.Mode) {
					case Mode.Normal:
						moduleCtx.ModeHandler = new NormalMode();
						break;
					case Mode.Dynamic:
						moduleCtx.ModeHandler = new DynamicMode();
						break;
					default:
						throw new UnreachableException();
				}

				if (!InjectHelpers(context, moduleCtx)) return;


				var cctor = context.CurrentModule.GlobalType.FindStaticConstructor();
				cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, moduleCtx.InitMethod));

				new MDPhase(moduleCtx).Hook(token);
			}
		}

		private bool InjectHelpers(IConfuserContext context, REContext moduleCtx) {
			Debug.Assert(context != null, $"{nameof(context)} != null");
			Debug.Assert(moduleCtx != null, $"{nameof(moduleCtx)} != null");

			var rtService = context.Registry.GetRequiredService<ProtectionsRuntimeService>();
			var rt = rtService.GetRuntimeModule();
			var name = context.Registry.GetRequiredService<INameService>();
			var constant = context.Registry.GetRequiredService<IConstantService>();
			var marker = context.Registry.GetRequiredService<IMarkerService>();
			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(ResourceProtection._Id);

			var rtInitMethod = GetInitMethod(moduleCtx.Module, context, rt, logger);
			if (rtInitMethod == null) return false;

			var dataType = new TypeDefUser("", "ConfuserResourceData",
				context.CurrentModule.CorLibTypes.GetTypeRef("System", "ValueType")) {
				Layout = TypeAttributes.ExplicitLayout,
				Visibility = TypeAttributes.NestedPrivate,
				IsSealed = true,
				ClassLayout = new ClassLayoutUser(1, 0)
			};
			moduleCtx.DataType = dataType;
			context.CurrentModule.GlobalType.NestedTypes.Add(dataType);
			name?.MarkHelper(context, dataType, marker, Parent);

			moduleCtx.DataField = new FieldDefUser("_ConfuserResourceData", new FieldSig(dataType.ToTypeSig())) {
				IsStatic = true,
				HasFieldRVA = true,
				InitialValue = new byte[0],
				Access = FieldAttributes.CompilerControlled
			};
			context.CurrentModule.GlobalType.Fields.Add(moduleCtx.DataField);
			name?.MarkHelper(context, moduleCtx.DataField, marker, Parent);


			var lateMutationKeys = ImmutableDictionary.Create<MutationField, LateMutationFieldUpdate>()
				.Add(MutationField.KeyI0, moduleCtx.loadSizeUpdate)
				.Add(MutationField.KeyI1, moduleCtx.loadSeedUpdate);

			var injectResult = rtService.InjectHelper.Inject(rtInitMethod, context.CurrentModule,
				InjectBehaviors.RenameAndNestBehavior(context, context.CurrentModule.GlobalType),
				new CompressionServiceProcessor(context, context.CurrentModule),
				new MutationProcessor(context.Registry, context.CurrentModule) {
					CryptProcessor = moduleCtx.ModeHandler.EmitDecrypt(moduleCtx),
					PlaceholderProcessor = (module, method, arg) => {
						var repl = new List<Instruction>(arg.Count + 3);
						repl.AddRange(arg);
						repl.Add(Instruction.Create(OpCodes.Dup));
						repl.Add(Instruction.Create(OpCodes.Ldtoken, moduleCtx.DataField));

						var runtimeHelper =
							context.CurrentModule.CorLibTypes.GetTypeRef("System.Runtime.CompilerServices",
								"RuntimeHelpers");
						var initArrayDef = runtimeHelper.ResolveThrow().FindMethod("InitializeArray");
						repl.Add(Instruction.Create(OpCodes.Call, moduleCtx.Module.Import(initArrayDef)));
						return repl;
					},
					LateKeyFieldValues = lateMutationKeys
				});

			moduleCtx.InitMethod = injectResult.Requested.Mapped;
			foreach (var member in injectResult) {
				name?.MarkHelper(context, member.Mapped, marker, Parent);
			}

			constant.ExcludeMethod(context, injectResult.Requested.Mapped);
			return true;
		}

		private static MethodDef GetInitMethod(ModuleDef module, IConfuserContext context, IRuntimeModule runtimeModule,
			ILogger logger) {
			Debug.Assert(module != null, $"{nameof(module)} != null");
			Debug.Assert(context != null, $"{nameof(context)} != null");
			Debug.Assert(runtimeModule != null, $"{nameof(runtimeModule)} != null");
			Debug.Assert(logger != null, $"{nameof(logger)} != null");

			string runtimeTypeName =
				context.Packer != null ? "Confuser.Runtime.Resource_Packer" : "Confuser.Runtime.Resource";
			;

			TypeDef rtType = null;
			try {
				rtType = runtimeModule.GetRuntimeType(runtimeTypeName, module);
			}
			catch (ArgumentException ex) {
				logger.LogError("Failed to load runtime: {0}", ex.Message);
				return null;
			}

			if (rtType == null) {
				logger.LogError("Failed to load runtime: {0}", runtimeTypeName);
				return null;
			}

			var initMethod = rtType.FindMethod("Initialize");
			if (initMethod == null) {
				logger.LogError("Could not find \"Initialize\" for {0}", rtType.FullName);
				return null;
			}

			return initMethod;
		}
	}
}
