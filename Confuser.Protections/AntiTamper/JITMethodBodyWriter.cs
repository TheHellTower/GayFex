using System;
using System.Diagnostics;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;

namespace Confuser.Protections.AntiTamper {
	internal sealed class JITMethodBodyWriter : MethodBodyWriterBase {
		private readonly CilBody _body;
		private readonly JITMethodBody _jitBody;
		private readonly bool _keepMaxStack;
		private readonly Metadata _metadata;

		public JITMethodBodyWriter(Metadata md, CilBody body, JITMethodBody jitBody, uint mulSeed, bool keepMaxStack) :
			base(body.Instructions, body.ExceptionHandlers) {
			_metadata = md;
			_body = body;
			_jitBody = jitBody;
			_keepMaxStack = keepMaxStack;
			_jitBody.MulSeed = mulSeed;
		}

		public void Write() {
			uint codeSize = InitializeInstructionOffsets();
			_jitBody.MaxStack = _keepMaxStack ? _body.MaxStack : GetMaxStack();

			_jitBody.Options = 0;
			if (_body.InitLocals)
				_jitBody.Options |= 0x10;

			if (_body.Variables.Count > 0) {
				var local = new LocalSig(_body.Variables.Select(var => var.Type).ToList());
				_jitBody.LocalVars = SignatureWriter.Write(_metadata, local);
			}
			else
				_jitBody.LocalVars = ReadOnlyMemory<byte>.Empty;

			{
				var newCode = new byte[codeSize];
				var writer = new ArrayWriter(newCode);
				uint writtenSize = WriteInstructions(ref writer);
				Debug.Assert(codeSize == writtenSize);
				_jitBody.ILCode = newCode;
			}

			_jitBody.ExceptionHandlers = new JITExceptionHandlerClause[exceptionHandlers.Count];
			if (exceptionHandlers.Count <= 0) return;

			_jitBody.Options |= 8;
			for (int i = 0; i < exceptionHandlers.Count; i++) {
				var eh = exceptionHandlers[i];
				_jitBody.ExceptionHandlers[i].Flags = (uint)eh.HandlerType;

				uint tryStart = GetOffset(eh.TryStart);
				uint tryEnd = GetOffset(eh.TryEnd);
				_jitBody.ExceptionHandlers[i].TryOffset = tryStart;
				_jitBody.ExceptionHandlers[i].TryLength = tryEnd - tryStart;

				uint handlerStart = GetOffset(eh.HandlerStart);
				uint handlerEnd = GetOffset(eh.HandlerEnd);
				_jitBody.ExceptionHandlers[i].HandlerOffset = handlerStart;
				_jitBody.ExceptionHandlers[i].HandlerLength = handlerEnd - handlerStart;

				switch (eh.HandlerType) {
					case ExceptionHandlerType.Catch: {
						uint token = _metadata.GetToken(eh.CatchType).Raw;
						if ((token & 0xff000000) == 0x1b000000)
							_jitBody.Options |= 0x80;

						_jitBody.ExceptionHandlers[i].ClassTokenOrFilterOffset = token;
						break;
					}
					case ExceptionHandlerType.Filter:
						_jitBody.ExceptionHandlers[i].ClassTokenOrFilterOffset = GetOffset(eh.FilterStart);
						break;
				}
			}
		}

		protected override void WriteInlineField(ref ArrayWriter writer, Instruction instr) =>
			writer.WriteUInt32(_metadata.GetToken(instr.Operand).Raw);

		protected override void WriteInlineMethod(ref ArrayWriter writer, Instruction instr) =>
			writer.WriteUInt32(_metadata.GetToken(instr.Operand).Raw);

		protected override void WriteInlineSig(ref ArrayWriter writer, Instruction instr) =>
			writer.WriteUInt32(_metadata.GetToken(instr.Operand).Raw);

		protected override void WriteInlineString(ref ArrayWriter writer, Instruction instr) =>
			writer.WriteUInt32(_metadata.GetToken(instr.Operand).Raw);

		protected override void WriteInlineTok(ref ArrayWriter writer, Instruction instr) =>
			writer.WriteUInt32(_metadata.GetToken(instr.Operand).Raw);

		protected override void WriteInlineType(ref ArrayWriter writer, Instruction instr) =>
			writer.WriteUInt32(_metadata.GetToken(instr.Operand).Raw);
	}
}
