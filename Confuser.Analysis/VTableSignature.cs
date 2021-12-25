using System;
using dnlib.DotNet;

namespace Confuser.Analysis {
	internal sealed class VTableSignature : IVTableSignature, IEquatable<VTableSignature> {
		internal VTableSignature(MethodSig sig, string name) {
			MethodSig = sig;
			Name = name;
		}

		public MethodSig MethodSig { get; private set; }
		public string Name { get; private set; }

		public static VTableSignature FromMethod(IMethod method) {
			MethodSig sig = method.MethodSig;
			TypeSig declType = method.DeclaringType.ToTypeSig();
			if (declType is GenericInstSig) {
				sig = GenericArgumentResolver.Resolve(sig, ((GenericInstSig)declType).GenericArguments);
			}

			return new VTableSignature(sig, method.Name);
		}

		public bool Equals(VTableSignature other) {
			if (other is null) return false;

			return new SigComparer().Equals(MethodSig, other.MethodSig) &&
				Name.Equals(other.Name, StringComparison.Ordinal);
		}

		public bool Equals(IVTableSignature other) => Equals(other as VTableSignature);

		public override bool Equals(object obj) => Equals(obj as VTableSignature);

		public override int GetHashCode() {
			int hash = 17;
			hash = hash * 7 + new SigComparer().GetHashCode(MethodSig);
			return hash * 7 + Name.GetHashCode();
		}

		public override string ToString() => FullNameFactory.MethodFullName("", Name, MethodSig);
	}
}
