using System;
using System.Text;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Confuser.UnitTest {
	public sealed class XunitLogger : ILogger, ILoggerProvider {
		private readonly ITestOutputHelper _outputHelper;
		private readonly StringBuilder _errorMessages;
		private readonly Action<string> _outputAction;

		public XunitLogger(ITestOutputHelper outputHelper) : this(outputHelper, null) { }

		public XunitLogger(ITestOutputHelper outputHelper, Action<string> outputAction) {
			_outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
			_errorMessages = new StringBuilder();
			_outputAction = outputAction;
		}

		public void CheckErrors() {
			if (_errorMessages.Length > 0)
				Assert.True(false, _errorMessages.ToString());
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
					_errorMessages.AppendLine(result);
					break;
			}

			_outputAction?.Invoke(result);
			_outputHelper.WriteLine(result);
		}

		private sealed class NullScope : IDisposable {
			internal static NullScope Instance { get; } = new NullScope();

			private NullScope() {
			}

			/// <inheritdoc />
			public void Dispose() {
			}
		}
	}
}
