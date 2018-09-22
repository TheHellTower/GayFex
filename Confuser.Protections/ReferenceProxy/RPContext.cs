using System.Collections.Generic;
using System.Collections.Immutable;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ReferenceProxy {
	internal enum Mode {
		Mild,
		Strong
	}

	internal enum EncodingType {
		Normal,
		Expression,
		x86
	}

	internal sealed class RPContext {
		internal ReferenceProxyProtection Protection;
		internal CilBody Body;
		internal IImmutableSet<Instruction> BranchTargets;
		internal IConfuserContext Context;
		internal Dictionary<MethodSig, TypeDef> Delegates;
		internal int Depth;
		internal IDynCipherService DynCipher;
		internal EncodingType Encoding;
		internal IRPEncoding EncodingHandler;
		internal int InitCount;
		internal bool InternalAlso;
		internal IMarkerService Marker;
		internal MethodDef Method;
		internal Mode Mode;

		internal RPMode ModeHandler;
		internal ModuleDef Module;
		internal INameService Name;
		internal IRandomGenerator Random;
		internal ITraceService Trace;
		internal bool TypeErasure;

		internal void MarkMember(IDnlibDef def) {
			if (Name == null) {
				Marker.Mark(Context, def, Protection);
			}
			else {
				Name.MarkHelper(Context, def, Marker, Protection);
			}
		}
	}
}
