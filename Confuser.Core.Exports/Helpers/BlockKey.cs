namespace Confuser.Core.Helpers {
	/// <summary>
	///     The information of the block in the key sequence
	/// </summary>
	public struct BlockKey {
		/// <summary>
		///     The state key at the beginning of the block
		/// </summary>
		public uint EntryState;

		/// <summary>
		///     The state key at the end of the block
		/// </summary>
		public uint ExitState;

		/// <summary>
		///     The type of block
		/// </summary>
		public BlockKeyType Type;
	}
}
