using System.Diagnostics.CodeAnalysis;

namespace Confuser.Runtime {
	/// <summary>Constant values for memory protection options.</summary>
	/// <remarks>https://docs.microsoft.com/de-de/windows/desktop/Memory/memory-protection-constants</remarks>
	[SuppressMessage("ReSharper", "InconsistentNaming")]
	internal static class MemoryProtectionConstants {
		internal const uint PAGE_EXECUTE_READWRITE = 0x40;
	}
}
