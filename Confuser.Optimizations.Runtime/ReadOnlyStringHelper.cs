using System;
using System.Collections.Generic;
using System.Text;

namespace Confuser.Optimizations.Runtime {
	public static class ReadOnlyStringHelper {
		public static ReadOnlyStringView GetView(string value, int start, int length) => new ReadOnlyStringView(value, start, length);

		public static int IndexOf(ReadOnlyStringView view, char value) => view.IndexOf(value);

		public static int IndexOfAny(ReadOnlyStringView view, char value1, char value2) => view.IndexOfAny(value1, value2);

		public static ref char GetReference(ReadOnlyStringView view) => ref view.GetReference();
	}
}
