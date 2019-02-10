using System;
using System.Runtime.Serialization;
using Confuser.Core;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	[Serializable]
	internal class RegexCompilerException : ConfuserException {
		[NonSerialized] private const string DefaultMessage = "Compiling an regular expression failed.";

		internal RegexCompilerException() : this(DefaultMessage) {
		}

		internal RegexCompilerException(string message) : base(message) {
		}

		internal RegexCompilerException(string message, Exception innerException) : base(message, innerException) {
		}

		internal RegexCompilerException(RegexCompileDef compileDef, Exception innerException) :
			this("Compiling the expression \"" + compileDef.Pattern + "\" failed.", innerException) {
		}

		protected RegexCompilerException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}
	}
}
