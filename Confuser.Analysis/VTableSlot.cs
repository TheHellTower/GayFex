using dnlib.DotNet;

namespace Confuser.Analysis {
	internal sealed class VTableSlot : IVTableSlot {
		internal VTableSlot(MethodDef def, TypeSig decl, VTableSignature signature)
			: this(def.DeclaringType.ToTypeSig(), def, decl, signature, null) {
		}

		internal VTableSlot(TypeSig defDeclType, MethodDef def, TypeSig decl, VTableSignature signature,
			VTableSlot? overrides) {
			MethodDefDeclType = defDeclType;
			MethodDef = def;
			DeclaringType = decl;
			Signature = signature;
			Overrides = overrides;
		}

		// This is the type in which this slot is defined.
		public TypeSig DeclaringType { get; internal set; }

		// This is the signature of this slot.
		public VTableSignature Signature { get; internal set; }


		// This is the method that is currently in the slot.
		public TypeSig MethodDefDeclType { get; private set; }
		public MethodDef MethodDef { get; private set; }

		// This is the 'parent slot' that this slot overrides.
		public VTableSlot? Overrides { get; private set; }

		IVTableSignature IVTableSlot.Signature => Signature;

		IVTableSlot? IVTableSlot.Overrides => Overrides;

		public VTableSlot OverridedBy(MethodDef method) =>
			new(method.DeclaringType.ToTypeSig(), method, DeclaringType, Signature, this);

		internal VTableSlot Clone() => new(MethodDefDeclType, MethodDef, DeclaringType, Signature, Overrides);

		public override string ToString() {
			return MethodDef.ToString();
		}
	}
}
