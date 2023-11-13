using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.Renamer;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using dnlib.DotNet.Pdb;
using dnlib.DotNet.Writer;

namespace Confuser.Protections.ControlFlow {
	internal class ControlFlowPhase : ProtectionPhase {
		static readonly JumpMangler Jump = new JumpMangler();
		static readonly SwitchMangler Switch = new SwitchMangler();
		
		public ControlFlowPhase(ControlFlowProtection parent)
			: base(parent) { }

		public override ProtectionTargets Targets {
			get { return ProtectionTargets.Methods; }
		}

		public override string Name {
			get { return "Control flow mangling"; }
		}

		CFContext ParseParameters(MethodDef method, ConfuserContext context, ProtectionParameters parameters, RandomGenerator random, bool disableOpti, CFType CFType) {
			var ret = new CFContext();
			ret.Type = parameters.GetParameter(context, method, "type", CFType);
			ret.Predicate = parameters.GetParameter(context, method, "predicate", PredicateType.x86);

			int rawIntensity = parameters.GetParameter(context, method, "intensity", 60);
			ret.Intensity = rawIntensity / 100.0;
			ret.Depth = parameters.GetParameter(context, method, "depth", 10);

			ret.JunkCode = parameters.GetParameter(context, method, "junk", true) && !disableOpti;

			ret.Protection = (ControlFlowProtection)Parent;
			ret.Random = random;
			ret.Method = method;
			ret.Context = context;
			ret.DynCipher = context.Registry.GetService<IDynCipherService>();

			if (ret.Predicate == PredicateType.x86) {
				if ((context.CurrentModule.Cor20HeaderFlags & ComImageFlags.ILOnly) != 0)
					context.CurrentModuleWriterOptions.Cor20HeaderOptions.Flags &= ~ComImageFlags.ILOnly;
			}

			return ret;
		}

		static bool DisabledOptimization(ModuleDef module) {
			bool disableOpti = false;
			CustomAttribute debugAttr = module.Assembly.CustomAttributes.Find("System.Diagnostics.DebuggableAttribute");
			if (debugAttr != null) {
				if (debugAttr.ConstructorArguments.Count == 1)
					disableOpti |= ((DebuggableAttribute.DebuggingModes)(int)debugAttr.ConstructorArguments[0].Value & DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0;
				else
					disableOpti |= (bool)debugAttr.ConstructorArguments[1].Value;
			}
			debugAttr = module.CustomAttributes.Find("System.Diagnostics.DebuggableAttribute");
			if (debugAttr != null) {
				if (debugAttr.ConstructorArguments.Count == 1)
					disableOpti |= ((DebuggableAttribute.DebuggingModes)(int)debugAttr.ConstructorArguments[0].Value & DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0;
				else
					disableOpti |= (bool)debugAttr.ConstructorArguments[1].Value;
			}
			return disableOpti;
		}

		protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
			bool disabledOpti = DisabledOptimization(context.CurrentModule);
			RandomGenerator random = context.Registry.GetService<IRandomService>().GetRandomGenerator(ControlFlowProtection._FullId);

			foreach (MethodDef method in parameters.Targets.OfType<MethodDef>().WithProgress(context.Logger))
				if (method.HasBody && method.Body.Instructions.Count > 0) {
					if(!method.Name.Contains("-UwU_OwO_UwU-")) {
						List<Instruction> SomeInstructons = ProcessMethod(method.Body, ParseParameters(method, context, parameters, random, disabledOpti, CFType.Switch));
						SelfProtection.ExecuteCFlow(method, SomeInstructons);
						if (method != context.CurrentModule.GlobalType.FindOrCreateStaticConstructor())
							ProcessMethod(method.Body, ParseParameters(method, context, parameters, random, disabledOpti, CFType.Jump));
						method.Body.SimplifyBranches();
						context.CheckCancellation();
					} else {
						var name = context.Registry.GetService<INameService>();
						method.Name = name.RandomName();
					}
				}
		}

		static ManglerBase GetMangler(CFType type) {
			if (type == CFType.Switch)
				return Switch;
			return Jump;
		}

		List<Instruction> ProcessMethod(CilBody body, CFContext ctx) {
			List<Instruction> toSend = new List<Instruction>();
			uint maxStack;
			if (!MaxStackCalculator.GetMaxStack(body.Instructions, body.ExceptionHandlers, out maxStack)) {
				ctx.Context.Logger.Error("Failed to calcuate maxstack.");
				throw new ConfuserException(null);
			}
			body.MaxStack = (ushort)maxStack;
			ScopeBlock root = BlockParser.ParseBody(body);

			toSend.AddRange(GetMangler(ctx.Type).Mangle(body, root, ctx));

			body.Instructions.Clear();
			root.ToBody(body);
			if (body.PdbMethod != null) {
				body.PdbMethod = new PdbMethod() {
					Scope = new PdbScope() {
						Start = body.Instructions.First(),
						End = body.Instructions.Last()
					}
				};
			}
			foreach (ExceptionHandler eh in body.ExceptionHandlers) {
				var index = body.Instructions.IndexOf(eh.TryEnd) + 1;
				eh.TryEnd = index < body.Instructions.Count ? body.Instructions[index] : null;
				index = body.Instructions.IndexOf(eh.HandlerEnd) + 1;
				eh.HandlerEnd = index < body.Instructions.Count ? body.Instructions[index] : null;
			}
			body.KeepOldMaxStack = true;

			return toSend;
		}
	}
}
