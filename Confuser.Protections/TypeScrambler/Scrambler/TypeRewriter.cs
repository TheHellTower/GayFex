using System.Diagnostics;
using Confuser.Core;
using Confuser.Protections.TypeScrambler.Scrambler.Rewriter.Instructions;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.TypeScrambler.Scrambler {
	internal sealed class TypeRewriter {
		private TypeService Service { get; }
		private InstructionRewriterFactory RewriteFactory { get; }

		internal TypeRewriter(IConfuserContext context) {
			Debug.Assert(context != null, $"{nameof(context)} != null");

			Service = context.Registry.GetRequiredService<TypeService>();

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

		internal void ProcessBody(MethodDef method) {
			Debug.Assert(method != null, $"{nameof(method)} != null");

			foreach (var local in method.Body.Variables) {
				local.Type = UpdateSignature(local.Type);
			}

			var il = method.Body.Instructions;
			for (int i = 0; i < il.Count; i++)
				RewriteFactory.Process(Service, method, il, ref i);
		}

		internal TypeSig UpdateSignature(TypeSig original) {
			var leaf = SignatureUtils.GetLeaf(original);
			if (leaf is TypeDefOrRefSig typeDefSig && typeDefSig.TypeDef != null) {
				var scannedDef = Service.GetItem(typeDefSig.TypeDef);
				if (scannedDef?.IsScambled == true) {
					TypeSig newSig = scannedDef.CreateGenericTypeSig(null);
					return SignatureUtils.CopyModifiers(original, newSig);
				}
			}

			return original;
		}
	}
}
