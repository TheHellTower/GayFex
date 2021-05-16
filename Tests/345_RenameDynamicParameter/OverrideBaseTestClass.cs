using System;

namespace RenameDynamicParameter {
	public abstract class OverrideBaseTestClass {
		protected void OverrideTestMethod(int strobj) => Console.WriteLine("Override Integer: " + strobj);
		protected abstract void OverrideTestMethod(string strobj);
	}
}
