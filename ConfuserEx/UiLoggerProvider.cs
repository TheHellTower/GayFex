using System;
using System.Windows.Documents;
using Microsoft.Extensions.Logging;

namespace ConfuserEx {
	internal sealed class UiLoggerProvider : ILoggerProvider {
		private Action<LogLevel, string> Publish { get; }
		private Func<LogLevel, bool> Filter { get; set; }

		internal UiLoggerProvider(Action<LogLevel, string> publish) =>
			Publish = publish ?? throw new ArgumentNullException(nameof(publish));

		ILogger ILoggerProvider.CreateLogger(string categoryName) =>
			new UiLogger(categoryName, Publish, Filter, false);

		void IDisposable.Dispose() { }
	}
}
