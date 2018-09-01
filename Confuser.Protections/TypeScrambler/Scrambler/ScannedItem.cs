using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Confuser.Renamer;
using dnlib.DotNet;

namespace Confuser.Protections.TypeScramble.Scrambler {
	internal abstract class ScannedItem {
		private readonly List<TypeSig> _trueTypes;

		private INameService NameService { get; }
		private IDictionary<TypeSig, GenericParam> Generics { get; }
		internal IReadOnlyList<TypeSig> TrueTypes => _trueTypes;

		private ushort GenericCount { get; set; }

		internal bool IsScambled => GenericCount > 0;

		protected ScannedItem(IGenericParameterProvider genericsProvider, INameService nameService) {
			Debug.Assert(genericsProvider != null, $"{nameof(genericsProvider)} != null");

			NameService = nameService;
			GenericCount = 0;
			Generics = new Dictionary<TypeSig, GenericParam>();
			_trueTypes = new List<TypeSig>();
		}

		internal bool RegisterGeneric(TypeSig t) {
			Debug.Assert(t != null, $"{nameof(t)} != null");
			if (t.IsSZArray) return false;

			if (!Generics.ContainsKey(t)) {
				Generics.Add(t, new GenericParamUser(GenericCount, GenericParamAttributes.NoSpecialConstraint, GetGenericParameterName(GenericCount)));
				GenericCount++;
				_trueTypes.Add(t);
				return true;
			}
			else {
				return false;
			}
		}

		private UTF8String GetGenericParameterName(ushort number) {
			if (NameService != null) return NameService.RandomName(RenameMode.ASCII);
			return $"TS{number}";
		}

		internal GenericMVar GetGeneric(TypeSig t) {
			Debug.Assert(t != null, $"{nameof(t)} != null");

			if (Generics.TryGetValue(t, out var gp))
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

		internal void PrepareGenerics() => PrepareGenerics(Generics.Values);

		protected abstract void PrepareGenerics(IEnumerable<GenericParam> scrambleParams);
		internal abstract IMemberDef GetMemberDef();

		internal abstract void Scan();
		internal abstract ClassOrValueTypeSig GetTarget();
	}
}
