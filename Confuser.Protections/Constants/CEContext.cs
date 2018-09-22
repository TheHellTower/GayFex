using System;
using System.Collections.Generic;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.Helpers;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Constants {
	internal sealed class CEContext {
		internal IConfuserContext Context;
		internal ConstantProtection Protection;
		internal ModuleDef Module;

		internal FieldDef DataField;
		internal TypeDef DataType;
		internal MethodDef InitMethod;

		internal uint DecoderCount;
		internal List<Tuple<MethodDef, DecoderDesc>> Decoders;

		internal EncodeElements Elements;
		internal List<uint> EncodedBuffer;

		internal Mode Mode;
		internal IEncodeMode ModeHandler;

		internal IDynCipherService DynCipher;
		internal IMarkerService Marker;
		internal INameService Name;
		internal IRandomGenerator Random;
		internal ITraceService Trace;

		internal TypeDef CfgCtxType;
		internal MethodDef CfgCtxCtor;
		internal MethodDef CfgCtxNext;
		internal Dictionary<MethodDef, List<Tuple<Instruction, uint, IMethod>>> ReferenceRepl;

		internal readonly LateMutationFieldUpdate EncodingBufferSizeUpdate = new LateMutationFieldUpdate();
		internal readonly LateMutationFieldUpdate KeySeedUpdate = new LateMutationFieldUpdate();
	}

	internal class DecoderDesc {
		public object Data;
		public byte InitializerID;
		public byte NumberID;
		public byte StringID;
	}
}
