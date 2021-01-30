using System;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Renamer.Analyzers {
	public sealed class ManifestResourceAnalyzer : IRenamer {
		/// <inheritdoc />
		public void Analyze(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def) { }

		/// <inheritdoc />
		void IRenamer.PreRename(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def) {
			if (!(def is MethodDef methodDef) || !methodDef.HasBody || !methodDef.Body.HasInstructions) return;

			var trace = context.Registry.GetService<ITraceService>();
			PreRename(context.CurrentModule, trace, methodDef);
		}

		public static void PreRename(ModuleDef currentModule, ITraceService trace, MethodDef methodDef) {
			var instructions = methodDef.Body.Instructions;
			var methodTrace = new Lazy<IMethodTrace>(() => trace.Trace(methodDef));
			for (var i = 0; i < instructions.Count; i++) {
				var instruction = instructions[i];
				if (instruction.OpCode != OpCodes.Callvirt ||
				    !(instruction.Operand is IMethodDefOrRef targetMethodDefOrRef) ||
				    !UTF8String.Equals(targetMethodDefOrRef.Name, "GetManifestResourceStream") ||
				    !UTF8String.Equals(targetMethodDefOrRef.DeclaringType.FullName, "System.Reflection.Assembly")) continue;

				var targetMethodDef = targetMethodDefOrRef.ResolveMethodDefThrow();
				if (targetMethodDef.Parameters.Count != 3) continue;

				var argumentIdx = methodTrace.Value.TraceArguments(instruction);
				if (argumentIdx.Length != 3) continue;

				var typeLoadInstruction = instructions[argumentIdx[1]];
				var resNameInstruction = instructions[argumentIdx[2]];
				
				if (IsGetTypeFromHandle(typeLoadInstruction)) continue;

				var typeLoadArguments = methodTrace.Value.TraceArguments(typeLoadInstruction);
				if (typeLoadArguments.Length != 1) continue;
				var typeTokenLoadInstruction = instructions[typeLoadArguments[0]];

				if (resNameInstruction.OpCode != OpCodes.Ldstr ||
				    !(resNameInstruction.Operand is string resName)) continue;
				
				if (typeTokenLoadInstruction.OpCode != OpCodes.Ldtoken ||
				    !(typeTokenLoadInstruction.Operand is ITypeDefOrRef refTypeDefOrRef)) continue;

				var resourceName = refTypeDefOrRef.Namespace + '.' + resName;

				var getManifestMethodDef = targetMethodDefOrRef.ResolveMethodDefThrow();
				var assemblyTypeDef = getManifestMethodDef.DeclaringType;
				var expectedSig = MethodSig.CreateInstance(getManifestMethodDef.MethodSig.RetType, getManifestMethodDef.MethodSig.Params.Last());
				var newMethodDef = assemblyTypeDef.FindMethod("GetManifestResourceStream", expectedSig);
				var newMethodRef = currentModule.Import(newMethodDef);
				
				resNameInstruction.Operand = resourceName;
				instruction.Operand = newMethodRef;

				instructions.RemoveAt(argumentIdx[1]);
				instructions.RemoveAt(typeLoadArguments[0]);
			}
		}

		private static bool IsGetTypeFromHandle(Instruction instruction) {
			return instruction.OpCode == OpCodes.Call &&
			       instruction.Operand is IMethodDefOrRef loadTypeMethodRef &&
			       UTF8String.Equals(loadTypeMethodRef.Name, "GetTypeFromHandle") &&
			       UTF8String.Equals(loadTypeMethodRef.DeclaringType.FullName, "System.Type");
		}

		/// <inheritdoc />
		public void PostRename(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def) { }
	}
}
