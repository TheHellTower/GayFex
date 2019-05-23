using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ReferenceProxy {
	/// <summary>
	/// Mild mode for the reference proxy protection.
	/// </summary>
	/// <remarks>
	/// This mode creates additional methods that wrap every single.
	/// </remarks>
	internal sealed class MildMode : RPMode {
		/// <summary>Contains the already created proxy methods for reusing.</summary>
		/// <remarks>
		///   <para>
		///     The key consists of:
		///     <list type="number">
		///       <item>
		///         <description>
		///           the OpCode that was used to call the original method that is either
		///           <c>call</c>, <c>calli</c>, <c>callvirt</c> or <c>newobj</c>
		///         </description>
		///       </item>
		///       <item>
		///         <description>
		///           The type that contains the method that is being processed and
		///           also the type that is going to contain the proxy method.
		///         </description>
		///       </item>
		///       <item>
		///         <description>
		///           The method that is being called by the generated proxy method.
		///         </description>
		///       </item>
		///     </list>
		///     The value of the dictionary is the proxy method that was generated.
		///   </para>
		/// </remarks>
		private readonly Dictionary<(Code OpCode, TypeDef CallingType, IMethod TargetMethod), MethodDef> _proxies =
			new Dictionary<(Code, TypeDef, IMethod), MethodDef>();

		private static UTF8String CreateProxyName((Code OpCode, TypeDef CallingType, IMethod TargetMethod) key) {
			var (_, callingType, targetMethod) = key;

			var targetMethodName = targetMethod.Name;
			if (targetMethodName.Length > 0 && targetMethodName.ToString()[0] == '.')
				targetMethodName = targetMethodName.Substring(1);

			UTF8String methodBaseName = "proxy_" + targetMethod.DeclaringType.Name + "_" + targetMethodName;
			var methodName = methodBaseName;
			var index = 1;
			while (callingType.FindMethod(methodName) != null)
				methodName = methodBaseName + "_" + index++;

			return methodName;
		}

		public override void ProcessCall(RPContext ctx, int instrIndex) {
			if (ctx == null) throw new ArgumentNullException(nameof(ctx));
			if (instrIndex < 0 || instrIndex >= ctx.Body.Instructions.Count)
				throw new ArgumentOutOfRangeException(nameof(instrIndex), instrIndex,
					"Instruction index is not within the legal range.");

			var invoke = ctx.Body.Instructions[instrIndex];
			if (!(invoke.Operand is IMethod target)) {
				Debug.Assert(false, $"{nameof(target)} of instruction is not a method.");
				return;
			}

			var targetDef = target.ResolveThrow();
			// Value type proxy is not supported in mild mode.
			if (targetDef.DeclaringType.IsValueType)
				return;
			// Skipping visibility is not supported in mild mode.
			if (!targetDef.IsPublic && !targetDef.IsAssembly)
				return;

			var key = (invoke.OpCode.Code, ctx.Method.DeclaringType, target);
			if (!_proxies.TryGetValue(key, out var proxy)) {
				var sig = CreateProxySignature(ctx, target, invoke.OpCode.Code == Code.Newobj);

				proxy = new MethodDefUser(CreateProxyName(key), sig) {
					Attributes = MethodAttributes.PrivateScope | MethodAttributes.Static,
					ImplAttributes = MethodImplAttributes.Managed | MethodImplAttributes.IL
				};
				ctx.Method.DeclaringType.Methods.Add(proxy);

				// Fix peverify --- Non-virtual call to virtual methods must be done on this pointer
				if (invoke.OpCode.Code == Code.Call && targetDef.IsVirtual) {
					proxy.IsStatic = false;
					sig.HasThis = true;
					sig.Params.RemoveAt(0);
				}

				ctx.MarkMember(proxy);

				proxy.Body = new CilBody();
				foreach (var param in proxy.Parameters)
					proxy.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, param));
				proxy.Body.Instructions.Add(Instruction.Create(invoke.OpCode, target));
				proxy.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

				_proxies[key] = proxy;
			}

			invoke.OpCode = OpCodes.Call;
			if (ctx.Method.DeclaringType.HasGenericParameters) {
				var genArgs = Enumerable.Range(0, ctx.Method.DeclaringType.GenericParameters.Count)
					.Select(i => new GenericVar(i))
					.Cast<TypeSig>()
					.ToArray();

				invoke.Operand = new MemberRefUser(
					ctx.Module,
					proxy.Name,
					proxy.MethodSig,
					new GenericInstSig(ctx.Method.DeclaringType.ToTypeSig().ToClassOrValueTypeSig(), genArgs)
						.ToTypeDefOrRef());
			}
			else
				invoke.Operand = proxy;

			ctx.Context.Annotations.Set(targetDef,
				ReferenceProxyProtection.Targeted, ReferenceProxyProtection.Targeted);
		}
	}
}
