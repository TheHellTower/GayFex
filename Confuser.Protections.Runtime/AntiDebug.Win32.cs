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
		private const string DnSpyDetectedMsg = "Detected dnspy";
#else
		private const string ManagedDebuggerActiveMsg = "";
		private const string IsDebuggerPresentMsg = "";
		private const string CurrentProcessMsg = "";
		private const string OutputDebugStringMsg = "";
		private const string CloseHandleMsg = "";
		private const string ThreadNotAliveMsg = "";
		private const string DnSpyDetectedMsg = "";
#endif

		public static void Initialize() {
			const string x = "COR";
			if (Environment.GetEnvironmentVariable(x + "_PROFILER") != null ||
			    Environment.GetEnvironmentVariable(x + "_ENABLE_PROFILING") != null)
				Environment.FailFast(null);
			//Anti dnspy
			Process here = GetParentProcess();
			if (!(here is null) && here.ProcessName.ToLower().Contains("dnspy"))
				Environment.FailFast(DnSpyDetectedMsg);

			var thread = new Thread(Worker) {IsBackground = true};
			thread.Start(null);
		}
		
		//https://stackoverflow.com/questions/394816/how-to-get-parent-process-in-net-in-managed-way

		private static ParentProcessUtilities PPU;
		public static Process GetParentProcess() {
			return ParentProcessUtilities.GetParentProcess();
		}

		/// <summary>
		/// A utility class to determine a process parent.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		internal struct ParentProcessUtilities {
			// These members must match PROCESS_BASIC_INFORMATION
			internal uint ExitStatus;
			internal IntPtr PebBaseAddress;
			internal UIntPtr AffinityMask;
			internal int BasePriority;
			internal UIntPtr UniqueProcessId;
			internal IntPtr InheritedFromUniqueProcessId;

			[DllImport("ntdll.dll")]
			private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref ParentProcessUtilities processInformation, int processInformationLength, out int returnLength);
			
			/// <summary>
			/// Gets the parent process of the current process.
			/// </summary>
			/// <returns>An instance of the Process class.</returns>
			internal static Process GetParentProcess() {
				return GetParentProcess(Process.GetCurrentProcess().Handle);
			}

			/// <summary>
			/// Gets the parent process of specified process.
			/// </summary>
			/// <param name="id">The process id.</param>
			/// <returns>An instance of the Process class.</returns>
			public static Process GetParentProcess(int id) {
				Process process = Process.GetProcessById(id);
				return GetParentProcess(process.Handle);
			}

			/// <summary>
			/// Gets the parent process of a specified process.
			/// </summary>
			/// <param name="handle">The process handle.</param>
			/// <returns>An instance of the Process class.</returns>
			public unsafe static Process GetParentProcess(IntPtr handle) {
				ParentProcessUtilities pbi = new ParentProcessUtilities();
				int returnLength;
				int status = NtQueryInformationProcess(handle, 0, ref pbi, sizeof(ParentProcessUtilities), out returnLength);
				if (status != 0)
					return null;

				try {
					return Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
				}
				catch (ArgumentException) {
					// not found
					return null;
				}
			}
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
