using dnlib.DotNet;

namespace Confuser.Analysis {
	public interface IVTableSlot {
		// This is the type in which this slot is defined.
		public TypeSig DeclaringType { get; }

		// This is the signature of this slot.
		public IVTableSignature Signature { get; }

		// This is the method that is currently in the slot.
		public TypeSig MethodDefDeclType { get; }
		public MethodDef MethodDef { get; }

		// This is the 'parent slot' that this slot overrides.
		public IVTableSlot Overrides { get; }
	}
}
