using System;
using Xunit.Abstractions;

namespace Confuser.UnitTest {
	public abstract class TestBase {
		protected readonly ITestOutputHelper outputHelper;

		public TestBase(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
	}
}
