using System;

namespace InheritCustomAttr {
	class D : C {
		// this property should inherit the MyAttribute from its base class
		public override DayOfWeek T { get => DayOfWeek.Monday; }
	}
}
