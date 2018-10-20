using System;
using System.Runtime.Serialization;

namespace Confuser.Core {
	/// <summary>
	///     The exception that is thrown when a handled error occurred during the protection process.
	/// </summary>
	[Serializable]
	public class ConfuserException : Exception {
		private const string DefaultMessage = "Exception occurred during the protection process.";

		public ConfuserException() : this(DefaultMessage) { }
		/// <summary>
		///     Initializes a new instance of the <see cref="ConfuserException" /> class.
		/// </summary>
		/// <param name="innerException">The inner exception, or null if no exception is associated with the error.</param>
		public ConfuserException(Exception innerException) : this(DefaultMessage, innerException) { }

		public ConfuserException(string message) : base(message) { }
		public ConfuserException(string message, Exception innerException) : base(message, innerException) { }
		protected ConfuserException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}
}
