using System;
using Xunit;
using Xunit.Abstractions;

namespace Confuser.Core.Services {
	public class RandomGeneratorExtensionsTest {
		private ITestOutputHelper OutputHelper { get; }

		public RandomGeneratorExtensionsTest(ITestOutputHelper outputHelper) =>
			OutputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Fact]
		[Trait("Category", "Core")]
		[Trait("Core", "random value generator")]
		public void TestRandomEnumValues() {
			var generator = new TestRandomEnumGenerator();

			Assert.Equal(TestRandomEnum.Value1, RandomGeneratorExtensions.NextMember<TestRandomEnum>(generator));
			Assert.Equal(TestRandomEnum.Value2, RandomGeneratorExtensions.NextMember<TestRandomEnum>(generator));
			Assert.Equal(TestRandomEnum.Value3, RandomGeneratorExtensions.NextMember<TestRandomEnum>(generator));
		}

		private sealed class TestRandomEnumGenerator : IRandomGenerator {
			private int _count = 0;

			public bool NextBoolean() => throw new InvalidOperationException(Messages.UnexpectedCallMsgFormat());

			public byte NextByte() => throw new InvalidOperationException(Messages.UnexpectedCallMsgFormat());

			public void NextBytes(Span<byte> buffer) {
				_count += 1;
				Assert.Equal(4, buffer.Length);
				switch (_count) {
					case 1:
						BitConverter.GetBytes(0).CopyTo(buffer);
						break;
					case 2:
						BitConverter.GetBytes(1).CopyTo(buffer);
						break;
					case 3:
						BitConverter.GetBytes(2).CopyTo(buffer);
						break;
					default:
						throw new InvalidOperationException(Messages.UnexpectedCallMsgFormat());
				}
			}
		}

		private enum TestRandomEnum {
			Value1,
			Value2,
			Value3
		}
	}
}
