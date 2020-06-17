using System.Diagnostics;
using Confuser.Core;
using Confuser.Protections.TypeScramble.Scrambler.Rewriter.Instructions;
using dnlib.DotNet;

namespace Confuser.Protections.TypeScramble.Scrambler {
	internal sealed class TypeRewriter {
		private TypeService Service { get; }
		private InstructionRewriterFactory RewriteFactory { get; }

		internal TypeRewriter(ConfuserContext context) {
			Debug.Assert(context != null, $"{nameof(context)} != null");

			Service = context.Registry.GetService<TypeService>();
			Debug.Assert(Service != null, $"{nameof(Service)} != null");

			RewriteFactory = new InstructionRewriterFactory() {
				new FieldDefInstructionRewriter(),
				new MethodSpecInstructionRewriter(),
				new MethodDefInstructionRewriter(),
				new MemberRefInstructionRewriter(),
				new TypeRefInstructionRewriter(),
				new TypeDefInstructionRewriter()
			};
		}

		internal void ApplyGenerics() => Service.PrepareItems();

		internal void Process(MethodDef method) {
			Debug.Assert(method != null, $"{nameof(method)} != null");

			// There is probably a better way to handle this
			var retDef = method.ReturnType.TryGetTypeDef();
			if (retDef != null) {
				var retType = Service.GetItem(retDef);
				if (retType?.IsScambled == true)
					method.MethodSig.RetType = retType.CreateGenericTypeSig(null);
			}
			
			var il = method.Body.Instructions;
			for (int i = 0; i < il.Count; i++)
				RewriteFactory.Process(Service, method, il, ref i);
		}

	}
}
