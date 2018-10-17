using System;
using antinet;

namespace Confuser.Runtime {
	public static partial class AntiDebugAntinet {
		public static void Initialize() {
			try {
				if (!AntiManagedDebugger.Initialize())
					Environment.FailFast(null);
			} catch { }

			try {
				AntiManagedProfiler.Initialize();
				if (AntiManagedProfiler.IsProfilerAttached) {
					Environment.FailFast(null);

					AntiManagedProfiler.PreventActiveProfilerFromReceivingProfilingMessages();
				}
			}
			catch { }
		}
	}
}
