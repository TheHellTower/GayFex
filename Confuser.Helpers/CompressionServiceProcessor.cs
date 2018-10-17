using System;
using System.Diagnostics;
using Confuser.Core;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Helpers {
	public sealed class CompressionServiceProcessor : IMethodInjectProcessor {
		private const string CompressionServiceTypeName = "Confuser.CompressionService";
		private const string DecompressionMethodName = "Decompress";

		private IConfuserContext Context { get; }
		private ICompressionService CompressionService { get; }
		private ModuleDef TargetModule { get; }


		public CompressionServiceProcessor(IConfuserContext context, ModuleDef targetModule) {
			Context = context ?? throw new ArgumentNullException(nameof(context));
			TargetModule = targetModule ?? throw new ArgumentNullException(nameof(targetModule));

			CompressionService = context.Registry.GetRequiredService<ICompressionService>();
		}

		void IMethodInjectProcessor.Process(MethodDef method) {
			Debug.Assert(method != null, $"{nameof(method)} != null");
			Debug.Assert(method.HasBody, $"{nameof(method)}.HasBody");

			if (method == null || !method.HasBody || !method.Body.HasInstructions) return;

			MethodDef decompressionMethod = null;
			foreach (var instr in method.Body.Instructions) {
				if (instr.OpCode == OpCodes.Call && instr.Operand is IMethod opMethod) {
					if (opMethod.Name == DecompressionMethodName && opMethod.DeclaringType.FullName == CompressionServiceTypeName) {
						if (decompressionMethod ==  null)
							decompressionMethod = CompressionService.GetRuntimeDecompressor(Context, TargetModule, def => { });

						instr.Operand = decompressionMethod;
					}
				}
			}
		}
	}
}
