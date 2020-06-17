using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Protections.TypeScramble.Scrambler {
	internal abstract class ScannedItem {
		private readonly List<TypeSig> _trueTypes;

		private IDictionary<TypeSig, GenericParam> Generics { get; }
		internal IReadOnlyList<TypeSig> TrueTypes => _trueTypes;

		private ushort GenericCount { get; set; }

		internal bool IsScambled => GenericCount > 0;

		protected ScannedItem(IGenericParameterProvider genericsProvider) {
			Debug.Assert(genericsProvider != null, $"{nameof(genericsProvider)} != null");

			GenericCount = 0;
			Generics = new Dictionary<TypeSig, GenericParam>(new TypeSigComparer());
			_trueTypes = new List<TypeSig>();
		}

		internal bool RegisterGeneric(TypeSig t) {
			Debug.Assert(t != null, $"{nameof(t)} != null");

			// This is a temporary fix.
			// Type visibility should be handled in a much better way which would involved some analysis.
			if (!t.ToTypeDefOrRef().ResolveTypeDef().IsVisibleOutside())
				return false;

			// Get proper type.
			t = GetLeaf(t);

			if (!Generics.ContainsKey(t)) {
				GenericParam newGenericParam;
				if (t.IsGenericMethodParameter) {
					var mVar = t.ToGenericMVar();
					Debug.Assert(mVar != null, $"{nameof(mVar)} != null");
					newGenericParam = new GenericParamUser(GenericCount, mVar.GenericParam.Flags, "T") {
						Rid = mVar.Rid
					};
				}
				else {
					newGenericParam = new GenericParamUser(GenericCount, GenericParamAttributes.NoSpecialConstraint, "T");
				}
				Generics.Add(t, newGenericParam);
				GenericCount++;
				_trueTypes.Add(t);
				return true;
			}
			else {
				return false;
			}
		}

		internal GenericSig GetGeneric(TypeSig t) {
			Debug.Assert(t != null, $"{nameof(t)} != null");

			t = GetLeaf(t);

			GenericSig result = null;
			if (Generics.TryGetValue(t, out var gp))
				result = this is ScannedType ? (GenericSig)new GenericVar(gp.Number) : new GenericMVar(gp.Number);

			return result;
		}

		internal TypeSig ConvertToGenericIfAvalible(TypeSig t) {
			Debug.Assert(t != null, $"{nameof(t)} != null");

			TypeSig newSig = GetGeneric(t);
			if (newSig != null) {
				// Now it may be that the signature contains lots of modifiers and signatures.
				// We need to process those... inside out.
				if (t is NonLeafSig) {
					// There are additional signatures. Store all of the in a stack and process them one by one.
					var sigStack = new Stack<NonLeafSig>();
					var current = t as NonLeafSig;
					while (current != null) {
						sigStack.Push(current);
						current = current.Next as NonLeafSig;
					}

					// Now process the entries on the stack one by one.
					while (sigStack.Any()) {
						current = sigStack.Pop();
						if (current is SZArraySig arraySig)
							newSig = new ArraySig(newSig, arraySig.Rank, arraySig.GetSizes(), arraySig.GetLowerBounds());
						else if (current is ByRefSig byRefSig)
							newSig = new ByRefSig(newSig);
						else if (current is CModReqdSig cModReqdSig)
							newSig = new CModReqdSig(cModReqdSig.Modifier, newSig);
						else if (current is CModOptSig cModOptSig)
							newSig = new CModOptSig(cModOptSig.Modifier, newSig);
						else if (current is PtrSig ptrSig)
							newSig = new PtrSig(newSig);
						else if (current is PinnedSig pinnedSig)
							newSig = new PinnedSig(newSig);
						else
							Debug.Fail("Unexpected leaf signature: " + current.GetType().FullName);
					}
				}
			}

			return newSig ?? t;
		}

		private static TypeSig GetLeaf(TypeSig t) {
			Debug.Assert(t != null, $"{nameof(t)} != null");

			while (t is NonLeafSig nonLeafSig)
				t = nonLeafSig.Next;

			return t;
		}

		internal void PrepareGenerics() => PrepareGenerics(Generics.Values.OrderBy(gp => gp.Number));

		protected abstract void PrepareGenerics(IEnumerable<GenericParam> scrambleParams);
		internal abstract IMemberDef GetMemberDef();

		internal abstract void Scan();
		internal abstract ClassOrValueTypeSig GetTarget();
	}
}
