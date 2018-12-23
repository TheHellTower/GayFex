using System.IO;

namespace System {
	internal class ApplicationException : Exception {
		internal ApplicationException() : base() { }
		internal ApplicationException(string message) : base(message) { }
		internal ApplicationException(string message, Exception innerException) : base(message, innerException) { }
	}
}

namespace SevenZip {
	internal static class StreamExtensions {
		internal static void Close(this Stream stream) => stream.Dispose();
	}
}
