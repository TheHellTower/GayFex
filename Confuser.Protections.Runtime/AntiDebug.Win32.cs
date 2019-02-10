using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Confuser.Runtime {
	public static class AntiDebugWin32 {
#if DEBUG
		private const string ManagedDebuggerActiveMsg = "Managed Debugger detected.";
		private const string IsDebuggerPresentMsg = "IsDebuggerPresent";
		private const string CurrentProcessMsg = "CurrentProcess";
		private const string OutputDebugStringMsg = "OutputDebugString";
		private const string CloseHandleMsg = "CloseHandle";
		private const string ThreadNotAliveMsg = "Thread is not alive";
#else
		private const string ManagedDebuggerActiveMsg = "";
		private const string IsDebuggerPresentMsg = "";
		private const string CurrentProcessMsg = "";
		private const string OutputDebugStringMsg = "";
		private const string CloseHandleMsg = "";
		private const string ThreadNotAliveMsg = "";
#endif

		public static void Initialize() {
			const string x = "COR";
			if (Environment.GetEnvironmentVariable(x + "_PROFILER") != null ||
			    Environment.GetEnvironmentVariable(x + "_ENABLE_PROFILING") != null)
				Environment.FailFast(null);

			var thread = new Thread(Worker) {IsBackground = true};
			thread.Start(null);
		}

		[DllImport("kernel32.dll")]
		private static extern bool CloseHandle(IntPtr hObject);

		[DllImport("kernel32.dll")]
		private static extern bool IsDebuggerPresent();

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
		private static extern void OutputDebugString([In, Optional] string str);

		private static void Worker(object thread) {
			if (!(thread is Thread th)) {
				th = new Thread(Worker) {IsBackground = true};
				th.Start(Thread.CurrentThread);
				Thread.Sleep(500);
			}

			while (true) {
				// Managed
				if (Debugger.IsAttached || Debugger.IsLogging())
					Environment.FailFast(ManagedDebuggerActiveMsg);

				// IsDebuggerPresent
				if (IsDebuggerPresent())
					Environment.FailFast(IsDebuggerPresentMsg);

				// OpenProcess
				using (var ps = Process.GetCurrentProcess()) {
					if (ps.Handle == IntPtr.Zero)
						Environment.FailFast(CurrentProcessMsg);
				}

				;

#if !NET20
				// OutputDebugString
				OutputDebugString("");
				if (Marshal.GetLastWin32Error() == 0)
					Environment.FailFast(OutputDebugStringMsg);
#endif

				// CloseHandle
				try {
					CloseHandle(IntPtr.Zero);
				}
				catch {
					Environment.FailFast(CloseHandleMsg);
				}

				if (!th.IsAlive)
					Environment.FailFast(ThreadNotAliveMsg);

				Thread.Sleep(1000);
			}
		}
	}
}
