using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
using Microsoft.Extensions.Logging;

namespace Confuser.Protections.ReferenceProxy {
	internal sealed class ReferenceProxyPhase : IProtectionPhase {
		public ReferenceProxyPhase(ReferenceProxyProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public ReferenceProxyProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Methods;

		public bool ProcessAll => false;

		public string Name => "Encoding reference proxies";

		private RPContext ParseParameters(MethodDef method, IConfuserContext context, IProtectionParameters parameters, RPStore store) {
			var ret = new RPContext {
				Mode = parameters.GetParameter(context, method, Parent.Parameters.Mode),
				Encoding = parameters.GetParameter(context, method, Parent.Parameters.Encoding),
				InternalAlso = parameters.GetParameter(context, method, Parent.Parameters.InternalAlso),
				TypeErasure = parameters.GetParameter(context, method, Parent.Parameters.EraseTypes),
				Depth = parameters.GetParameter(context, method, Parent.Parameters.Depth),

				Module = method.Module,
				Method = method,
				Body = method.Body,
				BranchTargets = method.Body.Instructions.SelectMany(InstructionOperands).ToImmutableHashSet(),

				Protection = Parent,
				Random = store.random,
				Context = context,
				Marker = context.Registry.GetRequiredService<IMarkerService>(),
				DynCipher = context.Registry.GetRequiredService<IDynCipherService>(),
				Name = context.Registry.GetService<INameService>(),
				Trace = context.Registry.GetRequiredService<ITraceService>(),

				Delegates = store.delegates
			};

			switch (ret.Mode) {
				case Mode.Mild:
					ret.ModeHandler = (store.mild ??= new MildMode());
					break;
				case Mode.Strong:
					ret.ModeHandler = (store.strong ??= new StrongMode(context));
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

		private static IEnumerable<Instruction> InstructionOperands(Instruction instr) {
			Debug.Assert(instr != null, $"{nameof(instr)} != null");

			if (instr.Operand is Instruction instrOp) {
				return ImmutableArray.Create(instrOp);
			}
			else if (instr.Operand is Instruction[] instrsOp) {
				return instrsOp;
			}

			return Enumerable.Empty<Instruction>();
		}

		private RPContext ParseParameters(ModuleDef module, IConfuserContext context, IProtectionParameters parameters,
			RPStore store) {
			var ret = new RPContext {
				Depth = parameters.GetParameter(context, module, Parent.Parameters.Depth),
				InitCount = parameters.GetParameter(context, module, Parent.Parameters.InitCount),

				Random = store.random,
				Module = module,
				Context = context,
				Marker = context.Registry.GetRequiredService<IMarkerService>(),
				DynCipher = context.Registry.GetRequiredService<IDynCipherService>(),
				Name = context.Registry.GetService<INameService>(),

				Delegates = store.delegates
			};

			return ret;
		}

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters,
			CancellationToken token) {
			var random = context.Registry.GetRequiredService<IRandomService>()
				.GetRandomGenerator(ReferenceProxyProtection._FullId);
			var logger = context.Registry.GetRequiredService<ILoggerFactory>()
				.CreateLogger(ReferenceProxyProtection._Id);

			var store = new RPStore {random = random};

			foreach (var method in parameters.Targets.OfType<MethodDef>()) //.WithProgress(logger))
				if (method.HasBody && method.Body.Instructions.Count > 0) {
					ProcessMethod(ParseParameters(method, context, parameters, store));
					token.ThrowIfCancellationRequested();
				}

			var ctx = ParseParameters(context.CurrentModule, context, parameters, store);

			store.strong?.Finalize(ctx);
		}

		private static void ProcessMethod(RPContext ctx) {
			if (ctx.Marker.GetHelperParent(ctx.Method) != null)
				return;

			for (int i = 0; i < ctx.Body.Instructions.Count; i++) {
				var instr = ctx.Body.Instructions[i];
				if (instr.OpCode.Code == Code.Call || instr.OpCode.Code == Code.Callvirt ||
				    instr.OpCode.Code == Code.Newobj) {
					var operand = (IMethod)instr.Operand;
					var def = operand.ResolveMethodDef();

					if (def != null &&
					    ctx.Context.Annotations.Get<object>(def, ReferenceProxyProtection.TargetExcluded) != null)
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
					var declType = operand.DeclaringType.ResolveTypeDefThrow();
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

		private sealed class RPStore {
			internal readonly Dictionary<MethodSig, TypeDef> delegates =
				new Dictionary<MethodSig, TypeDef>(new MethodSigComparer());

			internal ExpressionEncoding expression;
			internal MildMode mild;

			internal NormalEncoding normal;
			internal IRandomGenerator random;
			internal StrongMode strong;
			internal x86Encoding x86;

			private sealed class MethodSigComparer : IEqualityComparer<MethodSig> {
				bool IEqualityComparer<MethodSig>.Equals(MethodSig x, MethodSig y) => new SigComparer().Equals(x, y);

				int IEqualityComparer<MethodSig>.GetHashCode(MethodSig obj) => new SigComparer().GetHashCode(obj);
			}
		}
	}
}
