using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Confuser.Core;
using Confuser.Core.Project;
using GalaSoft.MvvmLight.Command;
using Microsoft.Extensions.Logging;

namespace ConfuserEx.ViewModel {
	internal class ProtectTabVM : TabViewModel {
		CancellationTokenSource cancelSrc;
		double? progress = 0;
		bool? result;
		private ConcurrentQueue<(LogLevel level, string message)> UnpublishedMessage { get; set; }
		private CancellationTokenSource TokenSource { get; set; }

		public ProtectTabVM(AppVM app)
			: base(app, "Protect!") {
			LogDocument = new FlowDocument();
		}

		public ICommand ProtectCmd {
			get { return new RelayCommand(DoProtect, () => !App.NavigationDisabled); }
		}

		public ICommand CancelCmd {
			get { return new RelayCommand(DoCancel, () => App.NavigationDisabled); }
		}

		public double? Progress {
			get { return progress; }
			set { SetProperty(ref progress, value, "Progress"); }
		}

		public FlowDocument LogDocument { get; private set; }

		public bool? Result {
			get { return result; }
			set { SetProperty(ref result, value, "Result"); }
		}

		private async void DoProtect() {
			var parameters = new ConfuserParameters {
				Project = ((IViewModel<ConfuserProject>)App.Project).Model,
				ConfigureLogging = builder =>
					builder.AddProvider(new UiLoggerProvider(SendLogMessage)).SetMinimumLevel(LogLevel.Information)
			};
			if (File.Exists(App.FileName))
				Environment.CurrentDirectory = Path.GetDirectoryName(App.FileName);

			cancelSrc = new CancellationTokenSource();
			Result = null;
			Progress = null;
			App.NavigationDisabled = true;

			UnpublishedMessage = new ConcurrentQueue<(LogLevel level, string message)>();
			TokenSource = new CancellationTokenSource();
			SendLogMessagesToUi();

			await ConfuserEngine.Run(parameters, cancelSrc.Token).ConfigureAwait(true);

			TokenSource.Cancel();
			Progress = 0;
			App.NavigationDisabled = false;
			CommandManager.InvalidateRequerySuggested();
		}

		void DoCancel() {
			cancelSrc.Cancel();
		}

		private void SendLogMessage(LogLevel level, string message) =>
			UnpublishedMessage.Enqueue((level, message));

		private async void SendLogMessagesToUi() {
			var token = TokenSource.Token;

			LogDocument.Blocks.Clear();
			var documentContent = new Paragraph();
			LogDocument.Blocks.Add(documentContent);

			try {
				for (;;) {
					var newMessages = 0;
					while (UnpublishedMessage.TryDequeue(out var log)) {
						var messageRun = new Run(log.message) {Foreground = GetLogLevelForeground(log.level)};

						documentContent.Inlines.Add(messageRun);
						documentContent.Inlines.Add(new LineBreak());

						newMessages++;
						if (newMessages >= 10) break;
					}

					if (token.IsCancellationRequested && newMessages < 10)
						return;

					await Task.Delay(80, token).ConfigureAwait(true);
				}
			}
			catch (OperationCanceledException) {
			}
		}

		private static Brush GetLogLevelForeground(LogLevel logLevel) {
			switch (logLevel) {
				case LogLevel.Trace:
					return Brushes.DarkGray;
				case LogLevel.Debug:
					return Brushes.Gray;
				case LogLevel.Information:
					return Brushes.White;
				case LogLevel.Warning:
					return Brushes.Yellow;
				case LogLevel.Error:
					return Brushes.IndianRed;
				case LogLevel.Critical:
					return Brushes.Red;
				default:
					throw new ArgumentOutOfRangeException(nameof(logLevel));
			}
		}
	}
}
