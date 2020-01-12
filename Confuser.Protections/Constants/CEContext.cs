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
	using BlockReferenceList = List<(MethodDef Method, Instruction Instruction, TypeSig TypeSig, int Size)>;

	internal sealed class CEContext {
		internal IConfuserContext Context;
		internal ConstantProtection Protection;
		internal ModuleDef Module;

		internal FieldDef DataField;
		internal TypeDef DataType;
		internal MethodDef InitMethod;

		internal uint DecoderCount;
		internal List<(MethodDef Method, DecoderDesc DecoderDesc)> Decoders;

		internal EncodeElements Elements;

		internal IEncryptMode EncryptMode { get; set; }
		internal IEncodeMode EncodeMode { get; set; }

		internal IDynCipherService DynCipher;
		internal IMarkerService Marker;
		internal INameService Name;
		internal IRandomGenerator Random;
		internal ITraceService Trace;

		internal TypeDef CfgCtxType;
		internal MethodDef CfgCtxCtor;
		internal MethodDef CfgCtxNext;

		internal CompressionAlgorithm UsedCompressionAlgorithm { get; set; }

		internal Memory<byte> EncodedData { get; set; }
		internal Dictionary<int, BlockReferenceList> EncodedReferences { get; set; }
		internal Dictionary<FieldDef, List<Instruction>> EncodedDataFields { get; set; }

		internal bool EncodeInitializers => (Elements & EncodeElements.Initializers) != 0;
		internal bool EncodeNumbers => (Elements & EncodeElements.Numbers) != 0;
		internal bool EncodePrimitive => (Elements & EncodeElements.Primitive) != 0;
		internal bool EncodeStrings => (Elements & EncodeElements.Strings) != 0;

		internal Dictionary<MethodDef, List<(Instruction TargetInstruction, uint Argument, IMethod DecoderMethod)>>
			ReferenceRepl;

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
