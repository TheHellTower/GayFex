using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.Protections.Services;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.Resources {
	internal class InjectPhase : IProtectionPhase {
		public InjectPhase(ResourceProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public ResourceProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Methods;

		public string Name => "Resource encryption helpers injection";

		public bool ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			if (parameters.Targets.Any()) {
				var logger = context.Registry.GetRequiredService<ILoggingService>().GetLogger("resources");
				if (!UTF8String.IsNullOrEmpty(context.CurrentModule.Assembly.Culture)) {
					logger.DebugFormat("Skipping resource encryption for satellite assembly '{0}'.",
									   context.CurrentModule.Assembly.FullName);
					return;
				}
				var compression = context.Registry.GetRequiredService<ICompressionService>();
				var name = context.Registry.GetService<INameService>();
				var marker = context.Registry.GetRequiredService<IMarkerService>();
				var rt = context.Registry.GetRequiredService<IRuntimeService>();
				var moduleCtx = new REContext {
					Random = context.Registry.GetRequiredService<IRandomService>().GetRandomGenerator(ResourceProtection._FullId),
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

				// Inject helpers
				MethodDef decomp = compression.GetRuntimeDecompressor(context, context.CurrentModule, member => {
					name?.MarkHelper(context, member, marker, Parent);
					if (member is MethodDef)
						context.GetParameters(member).RemoveParameters(Parent);
				});
				InjectHelpers(context, compression, rt, moduleCtx);

				// Mutate codes
				MutateInitializer(moduleCtx, decomp);

				MethodDef cctor = context.CurrentModule.GlobalType.FindStaticConstructor();
				cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, moduleCtx.InitMethod));

				new MDPhase(moduleCtx).Hook(token);
			}
		}

		void InjectHelpers(IConfuserContext context, ICompressionService compression, IRuntimeService rt, REContext moduleCtx) {
			var rtName = context.Packer != null ? "Confuser.Runtime.Resource_Packer" : "Confuser.Runtime.Resource";
			IEnumerable<IDnlibDef> members = InjectHelper.Inject(rt.GetRuntimeType(rtName), context.CurrentModule.GlobalType, context.CurrentModule);
			foreach (IDnlibDef member in members) {
				if (member.Name == "Initialize")
					moduleCtx.InitMethod = (MethodDef)member;
				moduleCtx.Name?.MarkHelper(context, member, moduleCtx.Marker, Parent);
			}

			var dataTypeName = moduleCtx.Name?.RandomName() ?? "ConfuserResourceData";
			var dataType = new TypeDefUser("", dataTypeName, context.CurrentModule.CorLibTypes.GetTypeRef("System", "ValueType")) {
				Layout = TypeAttributes.ExplicitLayout,
				Visibility = TypeAttributes.NestedPrivate,
				IsSealed = true,
				ClassLayout = new ClassLayoutUser(1, 0)
			};
			moduleCtx.DataType = dataType;
			context.CurrentModule.GlobalType.NestedTypes.Add(dataType);
			moduleCtx.Name?.MarkHelper(context, dataType, moduleCtx.Marker, Parent);

			var dataFieldName = moduleCtx.Name?.RandomName() ?? "_ConfuserResourceData";
			moduleCtx.DataField = new FieldDefUser(dataFieldName, new FieldSig(dataType.ToTypeSig())) {
				IsStatic = true,
				HasFieldRVA = true,
				InitialValue = new byte[0],
				Access = FieldAttributes.CompilerControlled
			};
			context.CurrentModule.GlobalType.Fields.Add(moduleCtx.DataField);
			moduleCtx.Name?.MarkHelper(context, moduleCtx.DataField, moduleCtx.Marker, Parent);
		}

		void MutateInitializer(REContext moduleCtx, MethodDef decomp) {
			moduleCtx.InitMethod.Body.SimplifyMacros(moduleCtx.InitMethod.Parameters);
			List<Instruction> instrs = moduleCtx.InitMethod.Body.Instructions.ToList();
			for (int i = 0; i < instrs.Count; i++) {
				Instruction instr = instrs[i];
				var method = instr.Operand as IMethod;
				if (instr.OpCode == OpCodes.Call) {
					if (method.DeclaringType.Name == "Mutation" &&
						method.Name == "Crypt") {
						Instruction ldBlock = instrs[i - 2];
						Instruction ldKey = instrs[i - 1];
						Debug.Assert(ldBlock.OpCode == OpCodes.Ldloc && ldKey.OpCode == OpCodes.Ldloc);
						instrs.RemoveAt(i);
						instrs.RemoveAt(i - 1);
						instrs.RemoveAt(i - 2);
						instrs.InsertRange(i - 2, moduleCtx.ModeHandler.EmitDecrypt(moduleCtx.InitMethod, moduleCtx, (Local)ldBlock.Operand, (Local)ldKey.Operand));
					}
					else if (method.DeclaringType.Name == "Lzma" &&
							 method.Name == "Decompress") {
						instr.Operand = decomp;
					}
				}
			}
			moduleCtx.InitMethod.Body.Instructions.Clear();
			foreach (Instruction instr in instrs)
				moduleCtx.InitMethod.Body.Instructions.Add(instr);

			MutationHelper.ReplacePlaceholder(moduleCtx.Trace, moduleCtx.InitMethod, arg => {
				var repl = new List<Instruction>();
				repl.AddRange(arg);
				repl.Add(Instruction.Create(OpCodes.Dup));
				repl.Add(Instruction.Create(OpCodes.Ldtoken, moduleCtx.DataField));
				repl.Add(Instruction.Create(OpCodes.Call, moduleCtx.Module.Import(
					typeof(RuntimeHelpers).GetMethod("InitializeArray"))));
				return repl.ToArray();
			});
			moduleCtx.Context.Registry.GetService<IConstantService>().ExcludeMethod(moduleCtx.Context, moduleCtx.InitMethod);
		}
	}
}
