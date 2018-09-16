using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;
using Confuser.Helpers;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.ReferenceProxy {
	internal class StrongMode : RPMode {
		// { invoke opCode, invoke target, encoding}, { proxy field, bridge method }
		readonly List<FieldDesc> fieldDescs = new List<FieldDesc>();
		readonly Dictionary<Tuple<Code, IMethod, IRPEncoding>, Tuple<FieldDef, MethodDef>> fields = new Dictionary<Tuple<Code, IMethod, IRPEncoding>, Tuple<FieldDef, MethodDef>>();

		readonly Dictionary<IRPEncoding, InitMethodDesc[]> inits = new Dictionary<IRPEncoding, InitMethodDesc[]>();
		RPContext encodeCtx;
		Tuple<TypeDef, Func<int, int>>[] keyAttrs;

		static int? TraceBeginning(RPContext ctx, int index, int argCount) {
			if (ctx.BranchTargets.Contains(ctx.Body.Instructions[index]))
				return null;

			int currentStack = argCount;
			int currentIndex = index;
			while (currentStack > 0) {
				currentIndex--;
				var currentInstr = ctx.Body.Instructions[currentIndex];

				// Disrupt stack analysis :/ Used by array initializer
				if (currentInstr.OpCode == OpCodes.Pop || currentInstr.OpCode == OpCodes.Dup)
					return null;

				// No branch instr.
				switch (currentInstr.OpCode.FlowControl) {
					case FlowControl.Call:
					case FlowControl.Break:
					case FlowControl.Meta:
					case FlowControl.Next:
						break;
					default:
						return null;
				}

				int push, pop;
				currentInstr.CalculateStackUsage(out push, out pop);
				currentStack += pop;
				currentStack -= push;

				// No branch target
				if (ctx.BranchTargets.Contains(currentInstr) && currentStack != 0)
					return null;
			}
			if (currentStack < 0)
				return null;
			return currentIndex;
		}

		public override void ProcessCall(RPContext ctx, int instrIndex) {
			var invoke = ctx.Body.Instructions[instrIndex];

			var declType = ((IMethod)invoke.Operand).DeclaringType.ResolveTypeDefThrow();
			if (!declType.Module.IsILOnly) // Reflection doesn't like mixed mode modules.
				return;
			if (declType.IsGlobalModuleType) // Reflection doesn't like global methods too.
				return;

			int push, pop;
			invoke.CalculateStackUsage(out push, out pop);
			int? begin = TraceBeginning(ctx, instrIndex, pop);
			// Fail to trace the arguments => fall back to bridge method
			bool fallBack = begin == null;

			if (fallBack) {
				ProcessBridge(ctx, instrIndex);
			}
			else {
				ProcessInvoke(ctx, instrIndex, begin.Value);
			}
		}

		void ProcessBridge(RPContext ctx, int instrIndex) {
			var instr = ctx.Body.Instructions[instrIndex];
			var target = (IMethod)instr.Operand;

			var declType = target.DeclaringType.ResolveTypeDefThrow();
			if (!declType.Module.IsILOnly) // Reflection doesn't like mixed mode modules.
				return;
			if (declType.IsGlobalModuleType) // Reflection doesn't like global methods too.
				return;

			var key = Tuple.Create(instr.OpCode.Code, target, ctx.EncodingHandler);
			Tuple<FieldDef, MethodDef> proxy;
			if (fields.TryGetValue(key, out proxy)) {
				if (proxy.Item2 != null) {
					instr.OpCode = OpCodes.Call;
					instr.Operand = proxy.Item2;
					return;
				}
			}
			else
				proxy = new Tuple<FieldDef, MethodDef>(null, null);

			var sig = CreateProxySignature(ctx, target, instr.OpCode.Code == Code.Newobj);
			var delegateType = GetDelegateType(ctx, sig);

			// Create proxy field
			if (proxy.Item1 == null)
				proxy = new Tuple<FieldDef, MethodDef>(
					CreateField(ctx, delegateType),
					proxy.Item2);

			// Create proxy bridge
			Debug.Assert(proxy.Item2 == null);

			proxy = new Tuple<FieldDef, MethodDef>(
				proxy.Item1,
				CreateBridge(ctx, delegateType, proxy.Item1, sig));

			fields[key] = proxy;

			// Replace instruction
			instr.OpCode = OpCodes.Call;
			instr.Operand = proxy.Item2;

			var targetDef = target.ResolveMethodDef();
			if (targetDef != null)
				ctx.Context.Annotations.Set(targetDef, ReferenceProxyProtection.Targeted, ReferenceProxyProtection.Targeted);
		}

		void ProcessInvoke(RPContext ctx, int instrIndex, int argBeginIndex) {
			var instr = ctx.Body.Instructions[instrIndex];
			var target = (IMethod)instr.Operand;

			var sig = CreateProxySignature(ctx, target, instr.OpCode.Code == Code.Newobj);
			var delegateType = GetDelegateType(ctx, sig);

			var key = Tuple.Create(instr.OpCode.Code, target, ctx.EncodingHandler);
			Tuple<FieldDef, MethodDef> proxy;
			if (!fields.TryGetValue(key, out proxy)) {
				// Create proxy field
				proxy = new Tuple<FieldDef, MethodDef>(CreateField(ctx, delegateType), null);
				fields[key] = proxy;
			}

			// Insert field load & replace instruction
			if (argBeginIndex == instrIndex) {
				ctx.Body.Instructions.Insert(instrIndex + 1,
											 new Instruction(OpCodes.Call, delegateType.FindMethod("Invoke")));
				instr.OpCode = OpCodes.Ldsfld;
				instr.Operand = proxy.Item1;
			}
			else {
				var argBegin = ctx.Body.Instructions[argBeginIndex];
				ctx.Body.Instructions.Insert(argBeginIndex + 1,
											 new Instruction(argBegin.OpCode, argBegin.Operand));
				argBegin.OpCode = OpCodes.Ldsfld;
				argBegin.Operand = proxy.Item1;

				instr.OpCode = OpCodes.Call;
				instr.Operand = delegateType.FindMethod("Invoke");
			}

			var targetDef = target.ResolveMethodDef();
			if (targetDef != null)
				ctx.Context.Annotations.Set(targetDef, ReferenceProxyProtection.Targeted, ReferenceProxyProtection.Targeted);
		}

		MethodDef CreateBridge(RPContext ctx, TypeDef delegateType, FieldDef field, MethodSig sig) {
			var method = new MethodDefUser(ctx.Name.RandomName(), sig);
			method.Attributes = MethodAttributes.PrivateScope | MethodAttributes.Static;
			method.ImplAttributes = MethodImplAttributes.Managed | MethodImplAttributes.IL;

			method.Body = new CilBody();
			method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldsfld, field));
			for (int i = 0; i < method.Parameters.Count; i++)
				method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, method.Parameters[i]));
			method.Body.Instructions.Add(Instruction.Create(OpCodes.Call, delegateType.FindMethod("Invoke")));
			method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

			delegateType.Methods.Add(method);

			ctx.Context.Registry.GetRequiredService<IMarkerService>().Mark(ctx.Context, method, ctx.Protection);
			ctx.Name?.SetCanRename(ctx.Context, method, false);

			return method;
		}

		FieldDef CreateField(RPContext ctx, TypeDef delegateType) {
			// Details will be filled in during metadata writing
			TypeDef randomType;
			do {
				randomType = ctx.Module.Types[ctx.Random.NextInt32(ctx.Module.Types.Count)];
			} while (randomType.HasGenericParameters || randomType.IsGlobalModuleType || randomType.IsDelegate());

			TypeSig fieldType = new CModOptSig(randomType, delegateType.ToTypeSig());

			var field = new FieldDefUser("", new FieldSig(fieldType), FieldAttributes.Static | FieldAttributes.Assembly);
			field.CustomAttributes.Add(new CustomAttribute(GetKeyAttr(ctx).FindInstanceConstructors().First()));
			delegateType.Fields.Add(field);

			ctx.Marker.Mark(ctx.Context, field, ctx.Protection);
			ctx.Name?.SetCanRename(ctx.Context, field, false);

			return field;
		}

		private TypeDef GetKeyAttr(RPContext ctx) {
			if (keyAttrs == null)
				keyAttrs = new Tuple<TypeDef, Func<int, int>>[0x10];

			int index = ctx.Random.NextInt32(keyAttrs.Length);
			if (keyAttrs[index] == null) {
				using (InjectHelper.CreateChildContext()) {
					var rt = ctx.Context.Registry.GetRequiredService<IRuntimeService>();
					var name = ctx.Context.Registry.GetService<INameService>();
					var marker = ctx.Context.Registry.GetRequiredService<IMarkerService>();

					ctx.DynCipher.GenerateExpressionPair(
						ctx.Random,
						new VariableExpression { Variable = new Variable("{VAR}") },
						new VariableExpression { Variable = new Variable("{RESULT}") },
						ctx.Depth, out var expression, out var inverse);

					var expCompiled = new DMCodeGen(typeof(int), new[] { Tuple.Create("{VAR}", typeof(int)) })
						.GenerateCIL(expression)
						.Compile<Func<int, int>>();

					var rtType = rt.GetRuntimeType("Confuser.Runtime.RefProxyKey");

					var injectResult = InjectHelper.Inject(rtType, ctx.Module,
						InjectBehaviors.RenameAndNestBehavior(ctx.Context, rtType),
						new MutationProcessor(ctx.Context.Registry, ctx.Module) {
							PlaceholderProcessor = (module, method, args) => {
								var invCompiled = new List<Instruction>();
								new CodeGen(args, module, method, invCompiled).GenerateCIL(inverse);
								return invCompiled;
							}
						});

					keyAttrs[index] = Tuple.Create(injectResult.Requested.Mapped, expCompiled);

					foreach (var def in injectResult)
						name?.MarkHelper(ctx.Context, def.Mapped, marker, ctx.Protection);
				}
			}
			return keyAttrs[index].Item1;
		}

		InitMethodDesc GetInitMethod(RPContext ctx, IRPEncoding encoding) {
			if (!inits.TryGetValue(encoding, out var initDescs))
				inits[encoding] = initDescs = new InitMethodDesc[ctx.InitCount];

			var rt = ctx.Context.Registry.GetRequiredService<IRuntimeService>();

			int index = ctx.Random.NextInt32(initDescs.Length);
			if (initDescs[index] == null) {
				using (InjectHelper.CreateChildContext()) {
					var rtType = rt.GetRuntimeType("Confuser.Runtime.RefProxyStrong");
					var rtInitMethod = rtType.FindMethod("Initialize");

					var desc = new InitMethodDesc();

					Span<int> order = stackalloc int[5];
					for (var i = 0; i < order.Length; i++) order[i] = i;
					ctx.Random.Shuffle(order);
					desc.OpCodeIndex = order[4];
					desc.TokenNameOrder = order.Slice(0, 4).ToArray();
					desc.TokenByteOrder = Enumerable.Range(0, 4).Select(x => x * 8).ToArray();
					ctx.Random.Shuffle(desc.TokenByteOrder);

					var mutationKeyValues = ImmutableDictionary.Create<MutationField, int>()
						.Add(MutationField.KeyI0, desc.TokenNameOrder[0])
						.Add(MutationField.KeyI1, desc.TokenNameOrder[1])
						.Add(MutationField.KeyI2, desc.TokenNameOrder[2])
						.Add(MutationField.KeyI3, desc.TokenNameOrder[3])
						.Add(MutationField.KeyI4, desc.TokenByteOrder[0])
						.Add(MutationField.KeyI5, desc.TokenByteOrder[1])
						.Add(MutationField.KeyI6, desc.TokenByteOrder[2])
						.Add(MutationField.KeyI7, desc.TokenByteOrder[3])
						.Add(MutationField.KeyI8, desc.OpCodeIndex);

					var injectResult = InjectHelper.Inject(rtInitMethod, ctx.Module,
						InjectBehaviors.RenameAndNestBehavior(ctx.Context, ctx.Module.GlobalType),
						new MutationProcessor(ctx.Context.Registry, ctx.Module) {
							KeyFieldValues = mutationKeyValues,
							PlaceholderProcessor = encoding.EmitDecode(ctx)
						});

					desc.Method = injectResult.Requested.Mapped;
					desc.Encoding = encoding;

					initDescs[index] = desc;
				}
			}
			return initDescs[index];
		}

		public override void Finalize(RPContext ctx) {
			foreach (var field in fields) {
				var init = GetInitMethod(ctx, field.Key.Item3);
				byte opKey;
				do {
					// No zero bytes
					opKey = ctx.Random.NextByte();
				} while (opKey == (byte)field.Key.Item1);

				var delegateType = field.Value.Item1.DeclaringType;

				var cctor = delegateType.FindOrCreateStaticConstructor();
				cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, init.Method));
				cctor.Body.Instructions.Insert(0, Instruction.CreateLdcI4(opKey));
				cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Ldtoken, field.Value.Item1));

				fieldDescs.Add(new FieldDesc {
					Field = field.Value.Item1,
					OpCode = field.Key.Item1,
					Method = field.Key.Item2,
					OpKey = opKey,
					InitDesc = init
				});
			}

			foreach (var delegateType in ctx.Delegates.Values) {
				var cctor = delegateType.FindOrCreateStaticConstructor();
				ctx.Marker.Mark(ctx.Context, cctor, ctx.Protection);
				ctx.Name?.SetCanRename(ctx.Context, cctor, false);
			}

			ctx.Context.CurrentModuleWriterOptions.MetadataOptions.Flags |= MetadataFlags.PreserveExtraSignatureData;
			ctx.Context.CurrentModuleWriterOptions.WriterEvent += EncodeField;
			encodeCtx = ctx;
		}

		void EncodeField(object sender, ModuleWriterEventArgs e) {
			var writer = (ModuleWriterBase)sender;
			if (e.Event == ModuleWriterEvent.MDMemberDefRidsAllocated && keyAttrs != null) {
				var keyFuncs = keyAttrs
					.Where(entry => entry != null)
					.ToDictionary(entry => entry.Item1, entry => entry.Item2);
				foreach (var desc in fieldDescs) {
					uint token = writer.Metadata.GetToken(desc.Method).Raw;
					uint key = encodeCtx.Random.NextUInt32() | 1;

					// CA
					var ca = desc.Field.CustomAttributes[0];
					int encodedKey = keyFuncs[(TypeDef)ca.AttributeType]((int)MathsUtils.modInv(key));
					ca.ConstructorArguments.Add(new CAArgument(encodeCtx.Module.CorLibTypes.Int32, encodedKey));
					token *= key;

					// Encoding
					token = (uint)desc.InitDesc.Encoding.Encode(desc.InitDesc.Method, encodeCtx, (int)token);

					// Field name
					Span<char> name = stackalloc char[5];
					name[desc.InitDesc.OpCodeIndex] = (char)((byte)desc.OpCode ^ desc.OpKey);

					Span<byte> nameKey = stackalloc byte[4];
					encodeCtx.Random.NextBytes(nameKey);
					uint encodedNameKey = 0;
					for (int i = 0; i < 4; i++) {
						// No zero bytes
						while (nameKey[i] == 0)
							nameKey[i] = encodeCtx.Random.NextByte();
						name[desc.InitDesc.TokenNameOrder[i]] = (char)nameKey[i];
						encodedNameKey |= (uint)nameKey[i] << desc.InitDesc.TokenByteOrder[i];
					}
					desc.Field.Name = name.ToString();

					// Field sig
					var sig = desc.Field.FieldSig;
					uint encodedToken = (token - writer.Metadata.GetToken(((CModOptSig)sig.Type).Modifier).Raw) ^ encodedNameKey;


					var extra = new byte[8];
					extra[0] = 0xc0;
					extra[3] = (byte)(encodedToken >> desc.InitDesc.TokenByteOrder[3]);
					extra[4] = 0xc0;
					extra[5] = (byte)(encodedToken >> desc.InitDesc.TokenByteOrder[2]);
					extra[6] = (byte)(encodedToken >> desc.InitDesc.TokenByteOrder[1]);
					extra[7] = (byte)(encodedToken >> desc.InitDesc.TokenByteOrder[0]);
					sig.ExtraData = extra;
				}
			}
		}

		private sealed class CodeGen : CILCodeGen {
			private readonly IReadOnlyList<Instruction> arg;

			internal CodeGen(IReadOnlyList<Instruction> arg, ModuleDef module, MethodDef method, IList<Instruction> instrs)
				: base(module, method, instrs) {
				this.arg = arg;
			}

			protected override void LoadVar(Variable var) {
				if (var.Name == "{RESULT}") {
					foreach (var instr in arg)
						Emit(instr);
				}
				else
					base.LoadVar(var);
			}
		}

		class FieldDesc {
			public FieldDef Field;
			public InitMethodDesc InitDesc;
			public IMethod Method;
			public Code OpCode;
			public byte OpKey;
		}

		class InitMethodDesc {
			public IRPEncoding Encoding;
			public MethodDef Method;
			public int OpCodeIndex;
			public int[] TokenByteOrder;
			public int[] TokenNameOrder;
		}
	}
}
