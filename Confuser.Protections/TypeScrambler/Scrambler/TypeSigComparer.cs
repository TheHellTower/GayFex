using System.Collections.Generic;
using dnlib.DotNet;

namespace Confuser.Protections.TypeScramble.Scrambler {
	internal sealed class TypeSigComparer : IEqualityComparer<TypeSig> {
		public bool Equals(TypeSig x, TypeSig y) => new SigComparer().Equals(x, y);

		public int GetHashCode(TypeSig obj) => new SigComparer().GetHashCode(obj);
	}
}
