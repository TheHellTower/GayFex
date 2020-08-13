using System;
using System.Threading.Tasks;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace EnhancedStrongName.Test {
	public class EnhancedStrongNameTest {
		private readonly ITestOutputHelper outputHelper;

		public EnhancedStrongNameTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Fact]
		[Trait("Category", "core")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/118")]
		public async Task SignatureMismatch() =>
			await TestRunner.Run("118_EnhancedStrongName.exe",
				new[] {"My strong key token: 79A18AF4CEA8A9BD", "My signature is valid!"},
				null,
				outputHelper,
				signWithKey: true);
	}
}
