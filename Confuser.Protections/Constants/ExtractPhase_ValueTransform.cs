using System.Runtime.InteropServices;

namespace Confuser.Protections.Constants {
	internal sealed partial class ExtractPhase {
		[StructLayout(LayoutKind.Explicit)]
		private struct RTransform {
			[FieldOffset(0)] internal long I8;
			[FieldOffset(0)] internal float R4;
			[FieldOffset(0)] internal double R8;

			[FieldOffset(4)] internal readonly int Hi;
			[FieldOffset(0)] internal readonly int Lo;
		}
	}
}