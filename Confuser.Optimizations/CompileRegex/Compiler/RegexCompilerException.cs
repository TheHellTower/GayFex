using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using Confuser.Core;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	[Serializable]
	[SuppressMessage("Microsoft.Design", "CA1032:ImplementStandardExceptionConstructors", 
		Justification = "Additional constructors are currently not required and will be added as needed.")]
	internal sealed class RegexCompilerException : ConfuserException {
		private RegexCompilerException(string message, Exception innerException) : base(message, innerException) {
		}

		internal RegexCompilerException(RegexCompileDef compileDef, Exception innerException) :
			this("Compiling the expression \"" + compileDef.Pattern + "\" failed.", innerException) {
		}

		private RegexCompilerException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}
	}
}
