using System;

namespace InheritCustomAttr {
	class D : C {
		// Just here to make sure it works.
		public event EventHandler<EventArgs> TestEvent;

		// this property should inherit the MyAttribute from its base class
		public override DayOfWeek T { get => DayOfWeek.Monday; }
	}
}
