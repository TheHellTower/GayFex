using System;
using dnlib.DotNet;

namespace Confuser.Analysis {
	public interface IVTableSignature : IEquatable<IVTableSignature> {
		public MethodSig MethodSig { get; }
		public string Name { get; }
	}
}
