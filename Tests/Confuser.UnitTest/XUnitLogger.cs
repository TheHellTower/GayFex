using System;
using Confuser.Core;
using Xunit.Abstractions;

namespace Confuser.UnitTest {
	public sealed class XunitLogger : ILogger {
		private readonly ITestOutputHelper outputHelper;

		public XunitLogger(ITestOutputHelper outputHelper) => 
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		void ILogger.Debug(string msg) => 
			outputHelper.WriteLine("[DEBUG] " + msg);

		void ILogger.DebugFormat(string format, params object[] args) =>
			outputHelper.WriteLine("[DEBUG] " + format, args);

		void ILogger.EndProgress() { }

		void ILogger.Error(string msg) => 
			throw new Exception(msg);

		void ILogger.ErrorException(string msg, Exception ex) => 
			throw new Exception(msg, ex);

		void ILogger.ErrorFormat(string format, params object[] args) => 
			throw new Exception(string.Format(format, args));

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
