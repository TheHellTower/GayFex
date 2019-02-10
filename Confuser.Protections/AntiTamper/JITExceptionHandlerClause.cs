namespace Confuser.Protections.AntiTamper {
	internal struct JITExceptionHandlerClause {
		public uint ClassTokenOrFilterOffset;
		public uint Flags;
		public uint HandlerLength;
		public uint HandlerOffset;
		public uint TryLength;
		public uint TryOffset;
	}
}
