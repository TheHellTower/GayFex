using System;
using System.Collections.Generic;
using System.Text;
using Confuser.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions.Internal;
using Xunit;
using Xunit.Abstractions;

namespace Confuser.UnitTest {
	public sealed class XunitLogger : ILogger, ILoggerProvider {
		private readonly ITestOutputHelper outputHelper;
		private readonly StringBuilder errorMessages;

		public XunitLogger(ITestOutputHelper outputHelper) {
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
			errorMessages = new StringBuilder();
		}

		public void CheckErrors() {
			if (errorMessages.Length > 0)
				Assert.True(false, errorMessages.ToString());
		}

		public ILogger CreateLogger(string categoryName) => this;

		public void Dispose() { }

		IDisposable ILogger.BeginScope<TState>(TState state) => NullScope.Instance;

		bool ILogger.IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

		void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) {
			var textBuilder = new StringBuilder();
			switch (logLevel) {
				case LogLevel.Critical: textBuilder.Append("[CRITICAL]"); break;
				case LogLevel.Debug: textBuilder.Append("[DEBUG]"); break;
				case LogLevel.Error: textBuilder.Append("[ERROR]"); break;
				case LogLevel.Information: textBuilder.Append("[INFO]"); break;
				case LogLevel.Trace: textBuilder.Append("[TRACE]"); break;
				case LogLevel.Warning: textBuilder.Append("[WARN]"); break;
			}
			textBuilder.Append(" ");
			textBuilder.Append(formatter(state, exception));

			if (exception != null) {
				textBuilder.AppendLine();
				textBuilder.Append(exception.ToString());
			}

			var result = textBuilder.ToString();
			switch (logLevel) {
				case LogLevel.Critical:
				case LogLevel.Error:
					errorMessages.AppendLine(result);
					break;
			}
			outputHelper.WriteLine(result);
		}
	}
}
