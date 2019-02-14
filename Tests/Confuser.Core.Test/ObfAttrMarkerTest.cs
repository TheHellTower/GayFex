using System;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace Confuser.Core {
	public class ObfAttrMarkerTest {
		private ITestOutputHelper OutputHelper { get; }

		public ObfAttrMarkerTest(ITestOutputHelper outputHelper) =>
			OutputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Fact]
		[Trait("Category", "Core")]
		[Trait("Core", "attribute parser")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/21")]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void TestIssue21() {
			const string testFeatureValue = "preset(aggressive);+rename(mode=decodable)";

			var result1 = ObfAttrMarker.ParseObfAttrFeatureValue(testFeatureValue);
			Assert.Equal("", result1.FeatureName);
			Assert.Equal(testFeatureValue, result1.FeatureValue);

			var result2 = ObfAttrMarker.ParseObfAttrFeatureValue("false:" + testFeatureValue);
			Assert.Equal("false", result2.FeatureName);
			Assert.Equal(testFeatureValue, result2.FeatureValue);
		}
	}
}
