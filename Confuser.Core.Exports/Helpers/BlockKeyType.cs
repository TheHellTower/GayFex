namespace Confuser.Core.Helpers {
	/// <summary>
	///     The type of block in the key sequence
	/// </summary>
	public enum BlockKeyType {
		/// <summary>
		///     The state key should be explicitly set in the block
		/// </summary>
		Explicit,

		/// <summary>
		///     The state key could be assumed to be same as <see cref="BlockKey.EntryState" /> at the beginning of block.
		/// </summary>
		Incremental
	}
}
