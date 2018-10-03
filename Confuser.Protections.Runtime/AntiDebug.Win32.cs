using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Confuser.Runtime {
	public static class AntiDebugWin32 {
		public static void Initialize() {
			const string x = "COR";
			if (Environment.GetEnvironmentVariable(x + "_PROFILER") != null ||
			    Environment.GetEnvironmentVariable(x + "_ENABLE_PROFILING") != null)
				Environment.FailFast(null);

			var thread = new Thread(Worker) { IsBackground = true };
			thread.Start(null);
		}

		[DllImport("kernel32.dll")]
		private static extern bool CloseHandle(IntPtr hObject);

		[DllImport("kernel32.dll")]
		private static extern bool IsDebuggerPresent();

#if NET20
		[DllImport("kernel32.dll")]
		private static extern int OutputDebugString(string str);
#else
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
		private static extern void OutputDebugString([In,Optional] string str);
#endif

		private static void Worker(object thread) {
			if (!(thread is Thread th)) {
				th = new Thread(Worker) { IsBackground = true };
				th.Start(Thread.CurrentThread);
				Thread.Sleep(500);
			}
			while (true) {
				// Managed
				if (Debugger.IsAttached || Debugger.IsLogging())
					Environment.FailFast("Managed Debugger detected.");

				// IsDebuggerPresent
				if (IsDebuggerPresent())
					Environment.FailFast("IsDebuggerPresent");

				// OpenProcess
				using (var ps = Process.GetCurrentProcess()) {
					if (ps.Handle == IntPtr.Zero)
						Environment.FailFast("CurrentProcess");
				};

				// OutputDebugString
#if NET20
				if (OutputDebugString("") > IntPtr.Size)
					Environment.FailFast("");
#else
				OutputDebugString("");
				if (Marshal.GetLastWin32Error() == 0)
					Environment.FailFast("OutputDebugString");
#endif

				// CloseHandle
				try {
					CloseHandle(IntPtr.Zero);
				}
				catch {
					Environment.FailFast("CloseHandle");
				}

				if (!th.IsAlive)
					Environment.FailFast("Thread is not alive");

				Thread.Sleep(1000);
			}
		}
	}
}
