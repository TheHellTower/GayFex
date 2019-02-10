using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions.Internal;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.MSBuild.Tasks {
	internal sealed class MSBuildLogger : ILogger, ILoggerProvider {
		private readonly TaskLoggingHelper loggingHelper;

		internal MSBuildLogger(TaskLoggingHelper loggingHelper) {
			this.loggingHelper = loggingHelper ?? throw new ArgumentNullException(nameof(loggingHelper));
		}

		public ILogger CreateLogger(string categoryName) => this;

		public void Dispose() {
		}

		IDisposable ILogger.BeginScope<TState>(TState state) => NullScope.Instance;

		bool ILogger.IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

		void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
			Func<TState, Exception, string> formatter) {
			var textBuilder = new StringBuilder();
			switch (logLevel) {
				case LogLevel.Critical:
					textBuilder.Append("[CRITICAL]");
					break;
				case LogLevel.Debug:
					textBuilder.Append("[DEBUG]");
					break;
				case LogLevel.Error:
					textBuilder.Append("[ERROR]");
					break;
				case LogLevel.Information:
					textBuilder.Append("[INFO]");
					break;
				case LogLevel.Trace:
					textBuilder.Append("[TRACE]");
					break;
				case LogLevel.Warning:
					textBuilder.Append("[WARN]");
					break;
			}

			textBuilder.Append(" ");
			textBuilder.Append(formatter(state, exception));

			var result = textBuilder.ToString();
			var importance = MessageImportance.Normal;
			switch (logLevel) {
				case LogLevel.Critical:
				case LogLevel.Error:
					importance = MessageImportance.High;
					break;
				case LogLevel.Information:
				case LogLevel.Debug:
					importance = MessageImportance.Low;
					break;
			}

			loggingHelper.LogMessage(importance, result);
		}
	}
}
