using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions.Internal;

namespace ConfuserEx {
	internal sealed class UiLogger : ILogger {
		private static readonly string _loglevelPadding = ": ";
		private static readonly string _messagePadding;
		private static readonly string _newLineWithMessagePadding;

		internal string Name { get; }
		internal Func<LogLevel, bool> Filter { get; }
		internal IExternalScopeProvider ScopeProvider { get; }
		private Action<LogLevel, string> Publish { get; }

		static UiLogger() {
			var maxLogLevelLength = 0;
			foreach (var level in Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>().Where(l => l != LogLevel.None)) {
				maxLogLevelLength = Math.Max(maxLogLevelLength, GetLogLevelString(level).Length);
			}
			_messagePadding = new string(' ', maxLogLevelLength + _loglevelPadding.Length);
			_newLineWithMessagePadding = Environment.NewLine + _messagePadding;
		}


		internal UiLogger(string name, Action<LogLevel, string> publish, Func<LogLevel, bool> filter, bool includeScopes)
			: this(name, publish, filter, includeScopes ? new LoggerExternalScopeProvider() : null) { }

		internal UiLogger(string name, Action<LogLevel, string> publish, Func<LogLevel, bool> filter, IExternalScopeProvider scopeProvider) {
			Name = name ?? throw new ArgumentNullException(nameof(name));
			Publish = publish ?? throw new ArgumentNullException(nameof(publish));
			Filter = filter ?? ((logLevel) => true);
			ScopeProvider = scopeProvider;
		}

		public IDisposable BeginScope<TState>(TState state) => ScopeProvider?.Push(state) ?? NullScope.Instance;

		public bool IsEnabled(LogLevel logLevel) {
			if (logLevel == LogLevel.None) return false;
			return Filter(logLevel);
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) {
			if (!IsEnabled(logLevel)) return;
			if (formatter == null) throw new ArgumentNullException(nameof(formatter));

			var message = formatter(state, exception);

			if (!string.IsNullOrEmpty(message) || exception != null) {
				WriteMessage(logLevel, Name, eventId.Id, message, exception);
			}
		}

		private void WriteMessage(LogLevel logLevel, string logName, int eventId, string message, Exception exception) {
			var logBuilder = new StringBuilder();
			var logLevelString = string.Empty;

			// Example:
			// INFO: ConsoleApp.Program[10]
			//       Request received

			// category and event id
			logBuilder.Append(GetLogLevelString(logLevel));
			logBuilder.Append(_messagePadding, logBuilder.Length, _messagePadding.Length - logBuilder.Length);
			//logBuilder.Append('(').Append(logName).Append(')');
			//if (eventId != 0) {
				//logBuilder.Append("[");
				//logBuilder.Append(eventId);
				//logBuilder.AppendLine("]");
			//}

			// scope information
			GetScopeInformation(logBuilder);

			if (!string.IsNullOrEmpty(message)) {
				// message
				logBuilder.Append(' ');

				var len = logBuilder.Length;
				logBuilder.Append(message);
				logBuilder.Replace(Environment.NewLine, _newLineWithMessagePadding, len, message.Length);
			}

			// Example:
			// System.InvalidOperationException
			//    at Namespace.Class.Function() in File:line X
			if (exception != null) {
				// exception message
				logBuilder.AppendLine(exception.ToString());
			}

			if (logBuilder.Length > 0) {
				var hasLevel = !string.IsNullOrEmpty(logLevelString);
				// Queue log message
				Publish(logLevel, logBuilder.ToString());
			}
		}

		private static string GetLogLevelString(LogLevel logLevel) {
			switch (logLevel) {
				case LogLevel.Trace:
					return "[TRACE]";
				case LogLevel.Debug:
					return "[DEBUG]";
				case LogLevel.Information:
					return "[INFO]";
				case LogLevel.Warning:
					return "[WARN]";
				case LogLevel.Error:
					return "[ERROR]";
				case LogLevel.Critical:
					return "[CRITICAL]";
				default:
					throw new ArgumentOutOfRangeException(nameof(logLevel));
			}
		}

		private void GetScopeInformation(StringBuilder stringBuilder) {
			var scopeProvider = ScopeProvider;
			if (scopeProvider != null) {
				var initialLength = stringBuilder.Length;

				scopeProvider.ForEachScope((scope, state) => {
					var (builder, length) = state;
					var first = length == builder.Length;
					builder.Append(first ? "=> " : " => ").Append(scope);
				}, (stringBuilder, initialLength));

				if (stringBuilder.Length > initialLength) {
					stringBuilder.Insert(initialLength, _messagePadding);
					stringBuilder.AppendLine();
				}
			}
		}
	}
}
