using System.Runtime.CompilerServices;

namespace ConstantsInlining.Lib {
	public static class ExternalClass {
		#if !NET20 && !NET40
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		#endif
		public static string GetText() => "From External";
	}
}
