using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Confuser.Runtime {
	internal static class AntiDebugSafe {
		[DllImport("kernel32.dll", EntryPoint = "ExitProcess")]
		public static extern void ExitProcess(uint uExitCode);

		static void Initialize(string GayFex) {
			uint exitCode = (uint)new Random().Next();
			string x = joined(new string[] { "C", "O", "R" });
			string y = joined(new string[] { "E", "N", "A", "B", "L", "E" });
			string z = joined(new string[] { "P", "R", "O", "F", "I", "L", "I", "N", "G" });
			const string xyz = "_";
			var env = typeof(Environment);
			var method = env.GetMethod(joined(new string[] { "G", "e", "t", "E", "n", "v", "i", "r", "o", "n", "m", "e", "n", "t", "V", "a", "r", "i", "a", "b", "l", "e" }), new[] { typeof(string) });

			// Comparison is done using is-operator to avoid the op_inequality overload of .NET 4.0
			// This is required to ensure that the result is .NET 2.0 compatible.
			if (!(method is null) &&
				"1".Equals(method.Invoke(null, new object[] { x + xyz + y + xyz + z })))
				ExitProcess(exitCode);

			var thread = new Thread(Worker);
			thread.IsBackground = true;
			thread.Start(null);

			string joined(string[] ToJoin) => string.Join(string.Empty, ToJoin);
		}

		static void Worker(object thread) {
			uint exitCode = (uint)new Random().Next();
			if (!(thread is Thread th)) {
				th = new Thread(Worker);
				th.IsBackground = true;
				th.Start(Thread.CurrentThread);
				Thread.Sleep(500);
			}
			while (true) {
				if (Debugger.IsAttached || Debugger.IsLogging())
					ExitProcess(exitCode);

				if (!th.IsAlive)
					ExitProcess(exitCode);

				Thread.Sleep(1000);
			}
		}
	}
}
