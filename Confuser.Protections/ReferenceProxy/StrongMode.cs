using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;
using Confuser.Helpers;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Protections.ReferenceProxy {
	internal sealed partial class StrongMode : RPMode {
		private readonly List<FieldDesc> _fieldDescs = new List<FieldDesc>();

		// { invoke opCode, invoke target, encoding}, { proxy field, bridge method }
		private readonly Dictionary<(Code OpCode, IMethod TargetMethod, IRPEncoding Encoding), (FieldDef Field,
			MethodDef BridgeMethod)> _fields
			= new Dictionary<(Code, IMethod, IRPEncoding), (FieldDef, MethodDef)>();

		private readonly Dictionary<IRPEncoding, InitMethodDesc[]> _initMethods =
			new Dictionary<IRPEncoding, InitMethodDesc[]>();

		private RPContext encodeCtx;

		private InjectHelper InjectHelper { get; }

		internal StrongMode(IConfuserContext context) : this((context ?? throw new ArgumentNullException(nameof(context))).Registry) { }

		internal StrongMode(IServiceProvider serviceProvider) {
			if (serviceProvider is null) throw new ArgumentNullException(nameof(serviceProvider));

			InjectHelper = serviceProvider.GetRequiredService<ProtectionsRuntimeService>().InjectHelper;
		}

		/// <summary>
		/// The instances of initialized key attributes that can be used.
		/// Each entry in the array marks the attribute that contains the decoder key as well as the
		/// compiled encoder function that is used to encode data with the key.
		/// </summary>
		private (TypeDef RefProxyKeyTypeDef, Func<int, int> EncodeFunc)[] _keyAttrs;

		private static int? TraceBeginning(RPContext ctx, int index, int argCount) {
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

				currentInstr.CalculateStackUsage(ctx.Method.HasReturnType, out var push, out var pop);
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
			var target = invoke.Operand as IMethod;
			Debug.Assert(target != null, $"{nameof(target)} of instruction is not a method.");
			if (target == null) return;

			var declType = target.DeclaringType.ResolveTypeDefThrow();
			if (!declType.Module.IsILOnly) // Reflection doesn't like mixed mode modules.
				return;
			if (declType.IsGlobalModuleType) // Reflection doesn't like global methods too.
				return;

			invoke.CalculateStackUsage(ctx.Method.HasReturnType, out var push, out var pop);
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

		private void ProcessBridge(RPContext ctx, int instrIndex) {
			var instr = ctx.Body.Instructions[instrIndex];
			if (!(instr.Operand is IMethod target)) {
				Debug.Assert(false, $"{nameof(target)} of instruction is not a method.");
				return;
			}

			var declType = target.DeclaringType.ResolveTypeDefThrow();
			if (!declType.Module.IsILOnly) // Reflection doesn't like mixed mode modules.
				return;
			if (declType.IsGlobalModuleType) // Reflection doesn't like global methods too.
				return;

			var key = (instr.OpCode.Code, target, ctx.EncodingHandler);
			if (_fields.TryGetValue(key, out var proxy)) {
				if (proxy.BridgeMethod != null) {
					instr.OpCode = OpCodes.Call;
					instr.Operand = proxy.BridgeMethod;
					return;
				}
			}

			var sig = CreateProxySignature(ctx, target, instr.OpCode.Code == Code.Newobj);
			var delegateType = GetDelegateType(ctx, sig);

			// Create proxy field
			if (proxy.Field == null)
				proxy = (CreateField(ctx, delegateType), proxy.BridgeMethod);

			if (proxy.Field == null)
				return;

			// Create proxy bridge
			Debug.Assert(proxy.BridgeMethod == null);

			proxy = (proxy.Field, CreateBridge(ctx, delegateType, proxy.Field, sig, key));

			_fields[key] = proxy;

			// Replace instruction
			instr.OpCode = OpCodes.Call;
			instr.Operand = proxy.BridgeMethod;

			var targetDef = target.ResolveMethodDef();
			if (targetDef != null)
				ctx.Context.Annotations.Set(targetDef, ReferenceProxyProtection.Targeted,
					ReferenceProxyProtection.Targeted);
		}

		private void ProcessInvoke(RPContext ctx, int instrIndex, int argBeginIndex) {
			var instr = ctx.Body.Instructions[instrIndex];
			if (!(instr.Operand is IMethod target)) {
				Debug.Assert(false, $"{nameof(target)} of instruction is not a method.");
				return;
			}

			var sig = CreateProxySignature(ctx, target, instr.OpCode.Code == Code.Newobj);
			var delegateType = GetDelegateType(ctx, sig);

			var key = (instr.OpCode.Code, target, ctx.EncodingHandler);
			if (!_fields.TryGetValue(key, out var proxy)) {
				// Create proxy field
				proxy = (CreateField(ctx, delegateType), null);
				_fields[key] = proxy;
			}

			if (proxy.Field == null)
				return;

			// Insert field load & replace instruction
			if (argBeginIndex == instrIndex) {
				ctx.Body.Instructions.Insert(instrIndex + 1,
					new Instruction(OpCodes.Call, delegateType.FindMethod("Invoke")));
				instr.OpCode = OpCodes.Ldsfld;
				instr.Operand = proxy.Field;
			}
			else {
				var argBegin = ctx.Body.Instructions[argBeginIndex];
				ctx.Body.Instructions.Insert(argBeginIndex + 1,
					new Instruction(argBegin.OpCode, argBegin.Operand));
				argBegin.OpCode = OpCodes.Ldsfld;
				argBegin.Operand = proxy.Field;

				instr.OpCode = OpCodes.Call;
				instr.Operand = delegateType.FindMethod("Invoke");
			}

			var targetDef = target.ResolveMethodDef();
			if (targetDef != null)
				ctx.Context.Annotations.Set(targetDef, ReferenceProxyProtection.Targeted,
					ReferenceProxyProtection.Targeted);
		}

		private MethodDef CreateBridge(
			RPContext ctx,
			TypeDef delegateType,
			FieldDef field,
			MethodSig sig,
			(Code OpCode, IMethod TargetMethod, IRPEncoding Encoding) key) {
			var method = new MethodDefUser(CreateProxyMethodName(delegateType, key), sig) {
				Attributes = MethodAttributes.PrivateScope | MethodAttributes.Static,
				ImplAttributes = MethodImplAttributes.Managed | MethodImplAttributes.IL,

				Body = new CilBody()
			};

			var instructions = method.Body.Instructions;
			instructions.Add(Instruction.Create(OpCodes.Ldsfld, field));
			foreach (var parameter in method.Parameters)
				instructions.Add(Instruction.Create(OpCodes.Ldarg, parameter));
			instructions.Add(Instruction.Create(OpCodes.Call, delegateType.FindMethod("Invoke")));
			instructions.Add(Instruction.Create(OpCodes.Ret));

			delegateType.Methods.Add(method);

			ctx.MarkMember(method);

			return method;
		}

		private FieldDef CreateField(RPContext ctx, TypeDef delegateType) {
			// Details will be filled in during metadata writing
			TypeDef randomType;
			do {
				randomType = ctx.Module.Types[ctx.Random.NextInt32(ctx.Module.Types.Count)];
			} while (randomType.HasGenericParameters || randomType.IsGlobalModuleType || randomType.IsDelegate());

			TypeSig fieldType = new CModOptSig(randomType, delegateType.ToTypeSig());

			var keyAttrType = GetKeyAttr(ctx);
			if (keyAttrType == null) return null;

			// The name of the field is set in the EncodeField function when the data is encoded.
			var field = new FieldDefUser(UTF8String.Empty, new FieldSig(fieldType),
				FieldAttributes.Static | FieldAttributes.Assembly);
			field.CustomAttributes.Add(new CustomAttribute(GetKeyAttr(ctx).FindInstanceConstructors().First()));
			delegateType.Fields.Add(field);

			ctx.MarkMember(field);

			return field;
		}

		private static UTF8String CreateProxyBaseName((Code OpCode, IMethod TargetMethod, IRPEncoding Encoding) key) {
			var (_, targetMethod, _) = key;

			var targetMethodName = targetMethod.Name;
			if (targetMethodName.Length > 0 && targetMethodName.ToString()[0] == '.')
				targetMethodName = targetMethodName.Substring(1);

			return "proxy_" + targetMethod.DeclaringType.Name + "_" + targetMethodName;
		}

		private static UTF8String CreateProxyMethodName(TypeDef delegateType, (Code OpCode, IMethod TargetMethod, IRPEncoding Encoding) key) {
			UTF8String methodBaseName = CreateProxyBaseName(key) + "_bridge";
			var methodName = methodBaseName;
			var index = 1;
			while (delegateType.FindMethod(methodName) != null)
				methodName = methodBaseName + "_" + index++;

			return methodName;
		}

		private TypeDef GetKeyAttr(RPContext ctx) {
			if (_keyAttrs == null)
				_keyAttrs = new (TypeDef, Func<int, int>)[0x10];

			int index = ctx.Random.NextInt32(_keyAttrs.Length);
			if (_keyAttrs[index].RefProxyKeyTypeDef != null)
				return _keyAttrs[index].RefProxyKeyTypeDef;

			using (InjectHelper.CreateChildContext()) {
				var marker = ctx.Context.Registry.GetRequiredService<IMarkerService>();

				var rtType = GetRuntimeType("Confuser.Runtime.RefProxyKey", ctx);
				if (rtType == null) return null;

				ctx.DynCipher.GenerateExpressionPair(
					ctx.Random,
					new VariableExpression { Variable = new Variable("{VAR}") },
					new VariableExpression { Variable = new Variable("{RESULT}") },
					ctx.Depth, out var expression, out var inverse);

				var expCompiled = new DMCodeGen(typeof(int), new[] { Tuple.Create("{VAR}", typeof(int)) })
					.GenerateCIL(expression)
					.Compile<Func<int, int>>();


				var injectResult = InjectHelper.Inject(rtType, ctx.Module,
					InjectBehaviors.RenameAndNestBehavior(ctx.Context, rtType),
					new MutationProcessor(ctx.Context.Registry, ctx.Module) {
						PlaceholderProcessor = (module, method, args) => {
							var invCompiled = new List<Instruction>();
							new CodeGen(args, module, method, invCompiled).GenerateCIL(inverse);
							return invCompiled;
						}
					});

				_keyAttrs[index] = (injectResult.Requested.Mapped, expCompiled);

				foreach (var (_, mapped) in injectResult)
					marker.Mark(ctx.Context, mapped, ctx.Protection);
			}

			return _keyAttrs[index].RefProxyKeyTypeDef;
		}

		private InitMethodDesc GetInitMethod(RPContext ctx, IRPEncoding encoding) {
			if (!_initMethods.TryGetValue(encoding, out var initDescs))
				_initMethods[encoding] = initDescs = new InitMethodDesc[ctx.InitCount];

			int index = ctx.Random.NextInt32(initDescs.Length);
			if (initDescs[index] != null) return initDescs[index];

			using (InjectHelper.CreateChildContext()) {
				var rtInitMethod = GetRuntimeInitMethod(ctx);
				if (rtInitMethod == null) return null;

				Span<int> nameOrder = stackalloc int[5] { 0, 1, 2, 3, 4 };
				Span<int> byteOrder = stackalloc int[4] { 0, 8, 16, 24 };
				ref int opCodeIndex = ref nameOrder[4];
				ctx.Random.Shuffle(nameOrder);
				ctx.Random.Shuffle(byteOrder);

				var mutationKeyValues = ImmutableDictionary.Create<MutationField, int>()
					.Add(MutationField.KeyI0, nameOrder[0])
					.Add(MutationField.KeyI1, nameOrder[1])
					.Add(MutationField.KeyI2, nameOrder[2])
					.Add(MutationField.KeyI3, nameOrder[3])
					.Add(MutationField.KeyI4, byteOrder[0])
					.Add(MutationField.KeyI5, byteOrder[1])
					.Add(MutationField.KeyI6, byteOrder[2])
					.Add(MutationField.KeyI7, byteOrder[3])
					.Add(MutationField.KeyI8, opCodeIndex);

				var injectResult = InjectHelper.Inject(rtInitMethod, ctx.Module,
					InjectBehaviors.RenameAndNestBehavior(ctx.Context, ctx.Module.GlobalType),
					new MutationProcessor(ctx.Context.Registry, ctx.Module) {
						KeyFieldValues = mutationKeyValues,
						PlaceholderProcessor = encoding.EmitDecode(ctx)
					});

				return initDescs[index] = new InitMethodDesc(encoding,
					injectResult.Requested.Mapped,
					opCodeIndex,
					byteOrder.ToArray(),
					nameOrder.Slice(0, 4).ToArray());
			}
		}

		private static TypeDef GetRuntimeType(string fullName, RPContext ctx) {
			Debug.Assert(fullName != null, $"{nameof(fullName)} != null");
			Debug.Assert(ctx != null, $"{nameof(ctx)} != null");

			var logger = ctx.Context.Registry.GetRequiredService<ILoggerFactory>()
				.CreateLogger(ReferenceProxyProtection._Id);
			return GetRuntimeType(fullName, ctx, logger);
		}

		private static TypeDef GetRuntimeType(string fullName, RPContext ctx, ILogger logger) {
			Debug.Assert(fullName != null, $"{nameof(fullName)} != null");
			Debug.Assert(ctx != null, $"{nameof(ctx)} != null");
			Debug.Assert(logger != null, $"{nameof(logger)} != null");

			var rt = ctx.Context.Registry.GetRequiredService<ProtectionsRuntimeService>().GetRuntimeModule();

			try {
				return rt.GetRuntimeType(fullName, ctx.Module);
			}
			catch (ArgumentException ex) {
				logger.LogError("Failed to load runtime: {0}", ex.Message);
			}

			return null;
		}

		private static MethodDef GetRuntimeInitMethod(RPContext ctx) {
			Debug.Assert(ctx != null, $"{nameof(ctx)} != null");

			var logger = ctx.Context.Registry.GetRequiredService<ILoggerFactory>()
				.CreateLogger(ReferenceProxyProtection._Id);
			var rtType = GetRuntimeType("Confuser.Runtime.RefProxyStrong", ctx, logger);
			if (rtType == null) return null;

			var initMethod = rtType.FindMethod("Initialize");
			if (initMethod == null) {
				logger.LogError("Could not find \"Initialize\" for {0}", rtType.FullName);
				return null;
			}

			return initMethod;
		}

		public void Finalize(RPContext ctx) {
			foreach (var field in _fields) {
				var init = GetInitMethod(ctx, field.Key.Encoding);
				if (init == null) return;

				byte opKey;
				do {
					// No zero bytes
					opKey = ctx.Random.NextByte();
				} while (opKey == (byte)field.Key.OpCode);

				var delegateType = field.Value.Field.DeclaringType;

				var cctor = delegateType.FindOrCreateStaticConstructor();
				cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, init.Method));
				cctor.Body.Instructions.Insert(0, Instruction.CreateLdcI4(opKey));
				cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Ldtoken, field.Value.Field));

				_fieldDescs.Add(new FieldDesc(field.Value.Field, init, field.Key.TargetMethod, field.Key.OpCode, opKey));
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

		private void EncodeField(object sender, ModuleWriterEventArgs e) {
			var writer = (ModuleWriterBase)sender;
			if (e.Event != ModuleWriterEvent.MDMemberDefRidsAllocated || _keyAttrs == null) return;

			var keyEncoderByAttrTypeDef = _keyAttrs
				.Where(entry => entry.RefProxyKeyTypeDef != null)
				.ToDictionary(entry => (ITypeDefOrRef)entry.RefProxyKeyTypeDef, entry => entry.EncodeFunc);
			foreach (var desc in _fieldDescs) {
				uint token = writer.Metadata.GetToken(desc.Method).Raw;
				uint key = encodeCtx.Random.NextUInt32() | 1;

				// CA - Add the Confuser.Runtime.RefProxyKey Attribute to the field
				// The parameter of the attribute is the modular inverse of the actual key passed through the
				// encoder function that is stored in the _keyAttrs array in this function.
				// The injected attribute type contains the required decoder function.
				var ca = desc.Field.CustomAttributes[0];
				int encodedKey = keyEncoderByAttrTypeDef[ca.AttributeType]((int)MathsUtils.ModInv(key));
				ca.ConstructorArguments.Add(new CAArgument(encodeCtx.Module.CorLibTypes.Int32, encodedKey));

				// Encoding
				// Apply the key to the actual token of the target method and encode the result.
				// The attribute will provide the required decoder and the result of the decoder is the
				// modular inverse of the key. Multiplying the decoded value to the token will restore the actual
				// token of the method.
				token *= key;
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
					name[desc.InitDesc.TokenNameOrder.Span[i]] = (char)nameKey[i];
					encodedNameKey |= (uint)nameKey[i] << desc.InitDesc.TokenByteOrder.Span[i];
				}

				desc.Field.Name = name.ToString();

				// Field sig
				// The base value for the key is the metadata token of a randomly chosen optional C modifier.
				var sig = desc.Field.FieldSig;
				uint encodedToken = (token - writer.Metadata.GetToken(((CModOptSig)sig.Type).Modifier).Raw) ^
									encodedNameKey;


				var extra = new byte[8];
				extra[0] = 0xc0;
				extra[3] = (byte)(encodedToken >> desc.InitDesc.TokenByteOrder.Span[3]);
				extra[4] = 0xc0;
				extra[5] = (byte)(encodedToken >> desc.InitDesc.TokenByteOrder.Span[2]);
				extra[6] = (byte)(encodedToken >> desc.InitDesc.TokenByteOrder.Span[1]);
				extra[7] = (byte)(encodedToken >> desc.InitDesc.TokenByteOrder.Span[0]);
				sig.ExtraData = extra;
			}
		}

		/// <summary>
		/// Create the delegate type for a specific method signature.
		/// </summary>
		/// <returns>The type of the injected delegate type.</returns>
		private static TypeDef GetDelegateType(RPContext ctx, MethodSig sig) {
			if (ctx.Delegates.TryGetValue(sig, out var ret))
				return ret;

			ret = new TypeDefUser(UTF8String.Empty, "proxy_delegate_" + ctx.Delegates.Count,
				ctx.Module.CorLibTypes.GetTypeRef("System", "MulticastDelegate")) {
				Attributes = TypeAttributes.NotPublic | TypeAttributes.Sealed
			};

			var ctor = new MethodDefUser(".ctor",
				MethodSig.CreateInstance(
					ctx.Module.CorLibTypes.Void,
					ctx.Module.CorLibTypes.Object,
					ctx.Module.CorLibTypes.IntPtr)) {
				Attributes = MethodAttributes.Assembly | MethodAttributes.HideBySig | MethodAttributes.RTSpecialName |
							 MethodAttributes.SpecialName,
				ImplAttributes = MethodImplAttributes.Runtime
			};
			ret.Methods.Add(ctor);

			var invoke = new MethodDefUser("Invoke", sig.Clone()) {
				MethodSig = { HasThis = true },
				Attributes = MethodAttributes.Assembly | MethodAttributes.HideBySig | MethodAttributes.Virtual |
							 MethodAttributes.NewSlot,
				ImplAttributes = MethodImplAttributes.Runtime
			};
			ret.Methods.Add(invoke);

			ctx.Module.Types.Add(ret);

			ret.FindOrCreateStaticConstructor();

			ctx.MarkMember(ret);
			foreach (var def in ret.FindDefinitions()) {
				ctx.Marker.Mark(ctx.Context, def, ctx.Protection);
				ctx.Name?.SetCanRename(ctx.Context, def, false);
			}

			ctx.Delegates[sig] = ret;
			return ret;
		}
	}
}
