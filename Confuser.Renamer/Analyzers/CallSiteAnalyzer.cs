using System.Linq;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Renamer.References;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Confuser.Renamer.Analyzers {
	internal sealed class CallSiteAnalyzer : IRenamer {
		public void Analyze(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def) {
			if (!(def is MethodDef method) || !method.HasBody)
				return;

			var traceService = context.Registry.GetRequiredService<ITraceService>();
			IMethodTrace methodTrace = null;

			var instructions = method.Body.Instructions;
			foreach (var instruction in instructions) {
				if (!IsCreateCallSiteInstruction(instruction)) continue;

				if (methodTrace is null)
					methodTrace = traceService.Trace(method);

				// CallSite`1.Create(CallSiteBinder)
				int[] createArguments = methodTrace.TraceArguments(instruction);
				if (createArguments.Length != 1) continue;

				// Binder.InvokeMember(CSharpBinderFlags, string, IEnumerable<Type>, Type, IEnumerable<CSharpArgumentInfo>)
				var binderInstruction = instructions[createArguments[0]];
				if (IsBinderInvokeMember(binderInstruction)) {
					HandleBinderInvokeMember(context, method, methodTrace, binderInstruction);
				}
			}
		}

		private static void HandleBinderInvokeMember(IConfuserContext context, MethodDef method, IMethodTrace methodTrace, Instruction instruction) {
			var instructions = method.Body.Instructions;

			int[] binderArguments = methodTrace.TraceArguments(instruction);
			if (binderArguments.Length != 5) return;

			var nameInstruction = instructions[binderArguments[1]];
			var contextInstruction = instructions[binderArguments[3]];

			// Name instruction is expected to contain a string constant - This is the name of the invoked member
			if (nameInstruction.OpCode.Code != Code.Ldstr) return;
			string boundMemberName = nameInstruction.Operand as string;

			var ldContextTokenInstruction = contextInstruction;
			if (IsGetTypeFromHandle(contextInstruction)) {
				int[] getTypeFromHandleArguments = methodTrace.TraceArguments(contextInstruction);
				if (getTypeFromHandleArguments.Length == 1)
					ldContextTokenInstruction = instructions[getTypeFromHandleArguments[0]];
			}

			if (ldContextTokenInstruction.OpCode.Code == Code.Ldtoken &&
				ldContextTokenInstruction.Operand is ITypeDefOrRef typeDefOrRef) {
				// We found the load token of the context parameter. This means we know the type the member is called for.
				BuildMemberReferences(context, typeDefOrRef, boundMemberName, nameInstruction);
			}
			else {
				var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(NameProtection._Id);
				logger.LogWarning(
					"Failed to resolve type for dynamic invoke member in {Method} - blocking all members with name {MemberName} from renaming",
					method, boundMemberName);

				// The type referenced is unknown. To be safe, all methods matching the name need to be blocked from renaming.
				DisableRenamingForMethods(context, boundMemberName);
			}
		}

		static void DisableRenamingForMethods(IConfuserContext context, string methodName) {
			var service = context.Registry.GetRequiredService<INameService>();

			var candidateMethods = context.Modules
				.SelectMany(m => m.FindDefinitions())
				.OfType<MethodDef>()
				.Where(m => m.Name.Equals(methodName));
			foreach (var candidateMethod in candidateMethods)
				service.SetCanRename(context, candidateMethod, false);
		}

		static void BuildMemberReferences(IConfuserContext context, ITypeDefOrRef typeDefOrRef, string boundMemberName,
			Instruction nameInstruction) {
			var service = context.Registry.GetRequiredService<INameService>();

			var boundMemberTypeDef = typeDefOrRef.ResolveTypeDef();
			if (boundMemberTypeDef is null) return;

			var currentType = boundMemberTypeDef;
			while (currentType != null) {
				foreach (var refMethod in currentType.FindMethods(boundMemberName)) {
					service.AddReference(context, refMethod,
						new StringMemberNameReference(nameInstruction, refMethod));
					service.ReduceRenameMode(context, refMethod, RenameMode.Reflection);
				}

				currentType = currentType.BaseType.ResolveTypeDef();
			}
		}

		private static bool IsCreateCallSiteInstruction(Instruction instruction) {
			if (instruction.OpCode.Code != Code.Call) return false;
			if (!(instruction.Operand is IMethodDefOrRef method)) return false;

			return method.DeclaringType.Namespace.Equals("System.Runtime.CompilerServices") &&
				   method.DeclaringType.Name.Equals("CallSite`1") &&
				   method.Name.Equals("Create");
		}

		private static bool IsBinderInvokeMember(Instruction instruction) {
			if (instruction.OpCode.Code != Code.Call) return false;
			if (!(instruction.Operand is IMethodDefOrRef method)) return false;

			return method.DeclaringType.Namespace.Equals("Microsoft.CSharp.RuntimeBinder") &&
				   method.DeclaringType.Name.Equals("Binder") &&
				   method.Name.Equals("InvokeMember");
		}

		private static bool IsGetTypeFromHandle(Instruction instruction) {
			if (instruction.OpCode.Code != Code.Call) return false;
			if (!(instruction.Operand is IMethodDefOrRef method)) return false;

			return method.DeclaringType.Namespace.Equals("System") &&
				   method.DeclaringType.Name.Equals("Type") &&
				   method.Name.Equals("GetTypeFromHandle");
		}

		public void PreRename(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def) { }

		public void PostRename(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def) { }
	}
}
