using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Confuser.Core;
using Confuser.Protections.TypeScramble.Scrambler.Analyzers;
using Confuser.Renamer;
using dnlib.DotNet;

namespace Confuser.Protections.TypeScramble.Scrambler {
	internal sealed class ScannedMethod : ScannedItem {

		internal MethodDef TargetMethod { get; }

		private ContextAnalyzerFactory Analyzers { get; }

		private bool ScramblePublicMethods { get; }

		internal ScannedMethod(TypeService service, INameService nameService, MethodDef target, bool scramblePublic) : base(target, nameService) {
			Debug.Assert(service != null, $"{nameof(service)} != null");
			Debug.Assert(target != null, $"{nameof(target)} != null");

			TargetMethod = target;
			ScramblePublicMethods = scramblePublic;

			Analyzers = new ContextAnalyzerFactory(this) {
				new MemberRefAnalyzer(),
				new TypeRefAnalyzer(),
				new MethodSpecAnalyzer(),
				new MethodDefAnalyzer(service)
			};
		}



		internal override void Scan() {
			// First we need to verify if it is actually acceptable to modify the method in any way.
			if (!CanScrambleMethod(TargetMethod, ScramblePublicMethods)) return;

			if (TargetMethod.HasBody) {
				foreach (var v in TargetMethod.Body.Variables) {
					RegisterGeneric(v.Type);
				}
			}

			if (TargetMethod.ReturnType != TargetMethod.Module.CorLibTypes.Void) {
				RegisterGeneric(TargetMethod.ReturnType);
			}
			foreach (var param in TargetMethod.Parameters) {
				if (!param.IsNormalMethodParameter) continue;

				RegisterGeneric(param.Type);
			}

			if (TargetMethod.HasBody)
				foreach (var i in TargetMethod.Body.Instructions)
					if (i.Operand != null)
						Analyzers.Analyze(i);
		}

		private static bool CanScrambleMethod(MethodDef method, bool scramblePublic) {
			Debug.Assert(method != null, $"{nameof(method)} != null");
			
			if (method.IsEntryPoint()) return false;
			if (method.HasOverrides || method.IsAbstract || method.IsConstructor || method.IsGetter || method.IsSetter) return false;

			// Resolving the references does not work in case the declaring type has generic paramters.
			if (method.DeclaringType.HasGenericParameters) return false;

			// Skip public visible methods is scrambling of public members is disabled.
			if (!scramblePublic && method.IsVisibleOutside()) return false;

			return true;
		}

		protected override void PrepareGenerics(IEnumerable<GenericParam> scrambleParams) {
			Debug.Assert(scrambleParams != null, $"{nameof(scrambleParams)} != null");
			if (!IsScambled) return;

			TargetMethod.GenericParameters.Clear();
			foreach (var generic in scrambleParams) {
				TargetMethod.GenericParameters.Add(generic);
			}

			if (TargetMethod.HasBody) {
				foreach (var v in TargetMethod.Body.Variables) {
					v.Type = ConvertToGenericIfAvalible(v.Type);
				}
			}

			foreach (var p in TargetMethod.Parameters) {
				if (p.Index == 0 && !TargetMethod.IsStatic) {
					continue;
				}
				p.Type = ConvertToGenericIfAvalible(p.Type);
			}

			if (TargetMethod.ReturnType != TargetMethod.Module.CorLibTypes.Void) {
				TargetMethod.ReturnType = ConvertToGenericIfAvalible(TargetMethod.ReturnType);
			}
		}

		internal GenericInstMethodSig CreateGenericMethodSig(ScannedMethod from, GenericInstMethodSig original = null) {
			var types = new List<TypeSig>(TrueTypes.Count);
			var processedGenericParams = 0;
			foreach (var trueType in TrueTypes) {
				if (trueType.IsGenericMethodParameter) {
					Debug.Assert(original != null, $"{nameof(original)} != null");
					var originalArgument = original.GenericArguments[processedGenericParams++];
					types.Add(originalArgument);
				} else if (from?.IsScambled == true) {
					types.Add(from.ConvertToGenericIfAvalible(trueType));
				} else {
					types.Add(trueType);
				}
			}

			return new GenericInstMethodSig(types);
		}

		internal override IMemberDef GetMemberDef() => TargetMethod;

		internal override ClassOrValueTypeSig GetTarget() => TargetMethod.DeclaringType.TryGetClassOrValueTypeSig();
	}
}
