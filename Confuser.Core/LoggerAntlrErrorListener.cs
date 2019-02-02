using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Microsoft.Extensions.Logging;

namespace Confuser.Core {
	internal class LoggerAntlrErrorListener<TSymbol> : IAntlrErrorListener<TSymbol> {
		private readonly ILogger _logger;

		internal LoggerAntlrErrorListener(ILogger logger) => 
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		public void SyntaxError(IRecognizer recognizer, TSymbol offendingSymbol, int line, int charPositionInLine,
			string msg, RecognitionException e) =>
			_logger.LogError(e, "Antlr Parse Error in line {line}:{charPos} - {msg}", line, charPositionInLine, msg);
	}
}
