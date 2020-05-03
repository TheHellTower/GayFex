using System;

namespace InheritCustomAttr {
	abstract class C : I<DayOfWeek> {
		[My(Value = 1)]
		public abstract DayOfWeek T { get; }
	}
}
