using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using dnlib.DotNet;

namespace Confuser.Protections.TypeScramble.Scrambler {
	internal abstract class ScannedItem {
		private readonly List<TypeSig> _trueTypes;

		private IDictionary<ITypeDefOrRef, GenericParam> Generics { get; }
		internal IReadOnlyList<TypeSig> TrueTypes => _trueTypes;

		private ushort GenericCount { get; set; }

		internal bool IsScambled => GenericCount > 0;

		protected ScannedItem(IGenericParameterProvider genericsProvider) {
			Debug.Assert(genericsProvider != null, $"{nameof(genericsProvider)} != null");
			
			GenericCount = 0;
			Generics = new Dictionary<ITypeDefOrRef, GenericParam>();
			_trueTypes = new List<TypeSig>();
		}

		internal bool RegisterGeneric(TypeSig t) {
			Debug.Assert(t != null, $"{nameof(t)} != null");
			if (t.IsSZArray) return false;

			var typeDef = t.ToTypeDefOrRef();
			Debug.Assert(typeDef != null, $"{nameof(typeDef)} != null");
			if (!Generics.ContainsKey(typeDef)) {
				Generics.Add(typeDef, new GenericParamUser(GenericCount, GenericParamAttributes.NoSpecialConstraint, "T"));
				GenericCount++;
				_trueTypes.Add(typeDef.ToTypeSig());
				return true;
			}
			else {
				return false;
			}
		}

		internal GenericMVar GetGeneric(TypeSig t) {
			Debug.Assert(t != null, $"{nameof(t)} != null");

			var typeDef = t.ToTypeDefOrRef();
			Debug.Assert(typeDef != null, $"{nameof(typeDef)} != null");
			if (Generics.TryGetValue(typeDef, out var gp))
				return new GenericMVar(gp.Number);

			return null;
		}

		internal TypeSig ConvertToGenericIfAvalible(TypeSig t) {
			Debug.Assert(t != null, $"{nameof(t)} != null");

			TypeSig newSig = GetGeneric(t);
			if (newSig != null && t.IsSingleOrMultiDimensionalArray) {
				if (!(t is SZArraySig tarr) || tarr.IsMultiDimensional) {
					newSig = null;
				}
				else {
					newSig = new ArraySig(newSig, tarr.Rank);
				}
			}
			return newSig ?? t;
		}

		internal void PrepareGenerics() => PrepareGenerics(Generics.Values.OrderBy(gp => gp.Number));

		protected abstract void PrepareGenerics(IEnumerable<GenericParam> scrambleParams);
		internal abstract IMemberDef GetMemberDef();

		internal abstract void Scan();
		internal abstract ClassOrValueTypeSig GetTarget();
	}
}
