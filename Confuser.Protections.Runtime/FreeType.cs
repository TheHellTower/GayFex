using System;

namespace Confuser.Runtime {
	[Flags]
	internal enum FreeType {
		Decommit = 0x4000,
		Release = 0x8000,
	}
}
