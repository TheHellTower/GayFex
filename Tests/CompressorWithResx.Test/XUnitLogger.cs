using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Confuser.Core;
using Xunit.Abstractions;

namespace CompressorWithResx.Test {
	internal sealed class XunitLogger : ILogger {
		private readonly ITestOutputHelper outputHelper;
		private readonly StringBuilder errorMessages;

		internal XunitLogger(ITestOutputHelper outputHelper) {
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
			errorMessages = new StringBuilder();
		}

		void ILogger.Debug(string msg) => 
			outputHelper.WriteLine("[DEBUG] " + msg);

		void ILogger.DebugFormat(string format, params object[] args) =>
			outputHelper.WriteLine("[DEBUG] " + format, args);

		void ILogger.EndProgress() { }

		void ILogger.Error(string msg) {
			outputHelper.WriteLine("[ERROR] " + msg);
			errorMessages.AppendLine(msg);
		}

		void ILogger.ErrorException(string msg, Exception ex) => 
			throw new Exception(msg, ex);

		void ILogger.ErrorFormat(string format, params object[] args) {
			outputHelper.WriteLine("[DEBUG] " + format, args);
			errorMessages.AppendLine(String.Format(format, args));
		}

		void ILogger.Finish(bool successful) =>
			outputHelper.WriteLine("[DONE]");

		void ILogger.Info(string msg) =>
			outputHelper.WriteLine("[INFO] " + msg);

		void ILogger.InfoFormat(string format, params object[] args) =>
			outputHelper.WriteLine("[INFO] " + format, args);

		void ILogger.Progress(int progress, int overall) { }

		void ILogger.Warn(string msg) =>
			outputHelper.WriteLine("[WARN] " + msg);

		void ILogger.WarnException(string msg, Exception ex) =>
			outputHelper.WriteLine("[WARN] " + msg + Environment.NewLine + ex.ToString());

		void ILogger.WarnFormat(string format, params object[] args) =>
			outputHelper.WriteLine("[WARN] " + format, args);
	}
}
