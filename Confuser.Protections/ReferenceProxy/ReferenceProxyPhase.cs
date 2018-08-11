using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.ReferenceProxy {
	internal sealed class ReferenceProxyPhase : IProtectionPhase {
		public ReferenceProxyPhase(ReferenceProxyProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public ReferenceProxyProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Methods;

		public bool ProcessAll => false;

		public string Name => "Encoding reference proxies";

		RPContext ParseParameters(MethodDef method, IConfuserContext context, IProtectionParameters parameters, RPStore store) {
			var ret = new RPContext();
			ret.Mode = parameters.GetParameter(context, method, Parent.Parameters.Mode);
			ret.Encoding = parameters.GetParameter(context, method, Parent.Parameters.Encoding);
			ret.InternalAlso = parameters.GetParameter(context, method, Parent.Parameters.InternalAlso);
			ret.TypeErasure = parameters.GetParameter(context, method, Parent.Parameters.EraseTypes);
			ret.Depth = parameters.GetParameter(context, method, Parent.Parameters.Depth);

			ret.Module = method.Module;
			ret.Method = method;
			ret.Body = method.Body;
			ret.BranchTargets = new HashSet<Instruction>(
				method.Body.Instructions
				      .Select(instr => instr.Operand as Instruction)
				      .Concat(method.Body.Instructions
				                    .Where(instr => instr.Operand is Instruction[])
				                    .SelectMany(instr => (Instruction[])instr.Operand))
				      .Where(target => target != null));

			ret.Protection = (ReferenceProxyProtection)Parent;
			ret.Random = store.random;
			ret.Context = context;
			ret.Marker = context.Registry.GetRequiredService<IMarkerService>();
			ret.DynCipher = context.Registry.GetRequiredService<IDynCipherService>();
			ret.Name = context.Registry.GetService<INameService>();
			ret.Trace = context.Registry.GetRequiredService<ITraceService>();

			ret.Delegates = store.delegates;

			switch (ret.Mode) {
				case Mode.Mild:
					ret.ModeHandler = store.mild ?? (store.mild = new MildMode());
					break;
				case Mode.Strong:
					ret.ModeHandler = store.strong ?? (store.strong = new StrongMode());
					break;
				default:
					throw new UnreachableException();
			}

			switch (ret.Encoding) {
				case EncodingType.Normal:
					ret.EncodingHandler = store.normal ?? (store.normal = new NormalEncoding());
					break;
				case EncodingType.Expression:
					ret.EncodingHandler = store.expression ?? (store.expression = new ExpressionEncoding());
					break;
				case EncodingType.x86:
					ret.EncodingHandler = store.x86 ?? (store.x86 = new x86Encoding());

					if ((context.CurrentModule.Cor20HeaderFlags & ComImageFlags.ILOnly) != 0)
						context.CurrentModuleWriterOptions.Cor20HeaderOptions.Flags &= ~ComImageFlags.ILOnly;
					break;
				default:
					throw new UnreachableException();
			}

			return ret;
		}

		RPContext ParseParameters(ModuleDef module, IConfuserContext context, IProtectionParameters parameters, RPStore store) {
			var ret = new RPContext();
			ret.Depth = parameters.GetParameter(context, module, Parent.Parameters.Depth);
			ret.InitCount = parameters.GetParameter(context, module, Parent.Parameters.InitCount);

			ret.Random = store.random;
			ret.Module = module;
			ret.Context = context;
			ret.Marker = context.Registry.GetRequiredService<IMarkerService>();
			ret.DynCipher = context.Registry.GetRequiredService<IDynCipherService>();
			ret.Name = context.Registry.GetService<INameService>();

			ret.Delegates = store.delegates;

			return ret;
		}

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			var random = context.Registry.GetRequiredService<IRandomService>().GetRandomGenerator(ReferenceProxyProtection._FullId);
			var logger = context.Registry.GetRequiredService<ILoggingService>().GetLogger("proxy");

			var store = new RPStore { random = random };

			foreach (MethodDef method in parameters.Targets.OfType<MethodDef>().WithProgress(logger))
				if (method.HasBody && method.Body.Instructions.Count > 0) {
					ProcessMethod(ParseParameters(method, context, parameters, store));
					token.ThrowIfCancellationRequested();
				}

			RPContext ctx = ParseParameters(context.CurrentModule, context, parameters, store);

			if (store.mild != null)
				store.mild.Finalize(ctx);

			if (store.strong != null)
				store.strong.Finalize(ctx);
		}

		void ProcessMethod(RPContext ctx) {
			for (int i = 0; i < ctx.Body.Instructions.Count; i++) {
				Instruction instr = ctx.Body.Instructions[i];
				if (instr.OpCode.Code == Code.Call || instr.OpCode.Code == Code.Callvirt || instr.OpCode.Code == Code.Newobj) {
					var operand = (IMethod)instr.Operand;
					var def = operand.ResolveMethodDef();

					if (def != null && ctx.Context.Annotations.Get<object>(def, ReferenceProxyProtection.TargetExcluded) != null)
						return;

					// Call constructor
					if (instr.OpCode.Code != Code.Newobj && operand.Name == ".ctor")
						continue;
					// Internal reference option
					if (operand is MethodDef && !ctx.InternalAlso)
						continue;
					// No generic methods
					if (operand is MethodSpec)
						continue;
					// No generic types / array types
					if (operand.DeclaringType is TypeSpec)
						continue;
					// No varargs
					if (operand.MethodSig.ParamsAfterSentinel != null &&
						operand.MethodSig.ParamsAfterSentinel.Count > 0)
						continue;
					TypeDef declType = operand.DeclaringType.ResolveTypeDefThrow();
					// No delegates
					if (declType.IsDelegate())
						continue;
					// No instance value type methods
					if (declType.IsValueType && operand.MethodSig.HasThis)
						continue;
					// No prefixed call
					if (i - 1 >= 0 && ctx.Body.Instructions[i - 1].OpCode.OpCodeType == OpCodeType.Prefix)
						continue;

					ctx.ModeHandler.ProcessCall(ctx, i);
				}
			}
		}

		class RPStore {
			public readonly Dictionary<MethodSig, TypeDef> delegates = new Dictionary<MethodSig, TypeDef>(new MethodSigComparer());
			public ExpressionEncoding expression;
			public MildMode mild;

			public NormalEncoding normal;
			public IRandomGenerator random;
			public StrongMode strong;
			public x86Encoding x86;

			class MethodSigComparer : IEqualityComparer<MethodSig> {
				public bool Equals(MethodSig x, MethodSig y) {
					return new SigComparer().Equals(x, y);
				}

				public int GetHashCode(MethodSig obj) {
					return new SigComparer().GetHashCode(obj);
				}
			}
		}
	}
}
