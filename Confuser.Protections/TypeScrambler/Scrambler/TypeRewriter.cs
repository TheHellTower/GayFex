using Confuser.Core;
using Confuser.Protections.Services;
using Confuser.Protections.TypeScramble.Scrambler.Rewriter.Instructions;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.TypeScramble.Scrambler {
	internal sealed class TypeRewriter {

		private IConfuserContext context;
		private TypeService Service;

		private InstructionRewriterFactory RewriteFactory;


		public TypeRewriter(IConfuserContext _context) {
			context = _context;
			Service = (TypeService)context.Registry.GetRequiredService<ITypeScrambleService>();

			RewriteFactory = new InstructionRewriterFactory() {
				new MethodSpecInstructionRewriter(),
				new MethodDefInstructionRewriter(),
				new MemberRefInstructionRewriter(_context),
				new TypeRefInstructionRewriter(),
				new TypeDefInstructionRewriter()
			};
		}

		public void ApplyGeterics() => Service.PrepairItems(); // Apply generics to sigs

		public void ImportCode(ModuleDef md) {
			//  ObjectCreationFactory.Import(md);
		}

		public void Process(MethodDef method) {

			var service = context.Registry.GetService<TypeService>();

			var il = method.Body.Instructions;

			for (int i = 0; i < il.Count; i++) {
				RewriteFactory.Process(service, method, il, i);
			}

		}

	}
}
