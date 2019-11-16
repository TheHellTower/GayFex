namespace Confuser.Core.Services {
	public enum CompressionMode {
		/// <summary>
		/// The automatic compression mode usually means that the protection tries to compress the data.
		/// In case the compressed data (including the required header) is smaller than the uncompressed data,
		/// the compressed data will be used. Otherwise the data is left uncompressed.
		/// </summary>
		Auto,

		/// <summary>
		/// The forced compression mode means that the data is always compressed, even if the compressed data
		/// is larger than the uncompressed data.
		/// </summary>
		Force,

		/// <summary>
		/// Just don't compress the data.
		/// </summary>
		Off
	}
}
