using System;
using System.Collections.Generic;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ReferenceProxy {
	internal class ExpressionEncoding : IRPEncoding {
		private delegate int EncodeKey(int key);

		private readonly Dictionary<MethodDef, (Expression DecodeExpression, EncodeKey EncodeFunction)> _keys =
			new Dictionary<MethodDef, (Expression, EncodeKey)>();

		Helpers.PlaceholderProcessor IRPEncoding.EmitDecode(RPContext ctx) => (module, method, args) => {
			var key = GetKey(ctx, method);

			var invCompiled = new List<Instruction>();
			new CodeGen(args, module, method, invCompiled).GenerateCIL(key.DecodeExpression);
			method.Body.MaxStack += (ushort)ctx.Depth;
			return invCompiled.ToArray();
		};

		public int Encode(MethodDef init, RPContext ctx, int value) {
			var key = GetKey(ctx, init);
			return key.EncodeFunction(value);
		}

		private static void Compile(RPContext ctx, CilBody body, out EncodeKey expCompiled, out Expression inverse) {
			var var = new Variable("{VAR}");
			var result = new Variable("{RESULT}");

			ctx.DynCipher.GenerateExpressionPair(
				ctx.Random,
				new VariableExpression {Variable = var}, new VariableExpression {Variable = result},
				ctx.Depth, out var expression, out inverse);

			expCompiled = new DMCodeGen(typeof(int), new[] {Tuple.Create("{VAR}", typeof(int))})
				.GenerateCIL(expression)
				.Compile<EncodeKey>();
		}

		private (Expression DecodeExpression, EncodeKey EncodeFunction) GetKey(RPContext ctx, MethodDef init) {
			if (_keys.TryGetValue(init, out var ret)) return ret;

			Compile(ctx, init.Body, out var keyFunc, out var inverse);
			_keys[init] = ret = (inverse, keyFunc);

			return ret;
		}

		private sealed class CodeGen : CILCodeGen {
			private readonly IReadOnlyList<Instruction> _arg;

			internal CodeGen(IReadOnlyList<Instruction> arg, ModuleDef module, MethodDef method,
				IList<Instruction> instrs)
				: base(module, method, instrs) =>
				_arg = arg;

			protected override void LoadVar(Variable var) {
				if (var.Name == "{RESULT}") {
					foreach (var instr in _arg)
						Emit(instr);
				}
				else
					base.LoadVar(var);
			}
		}
	}
}
