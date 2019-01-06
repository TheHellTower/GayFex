using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Confuser.Core;
using Xunit;

namespace Confuser.Optimizations.TailCall {
	public static class TailCallParametersTest {
		[Fact]
		[Trait("Category", "Optimization")]
		[Trait("Optimization", TailCallProtection.Id)]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void ParametersInDictionaryTest() {
			var parameters = new TailCallProtectionParameters();
			IReadOnlyDictionary<string, IProtectionParameter> paramDict = parameters;

			Assert.Contains(CreateEntry(parameters.TailRecursion), paramDict);
		}

		private static KeyValuePair<string, IProtectionParameter> CreateEntry(IProtectionParameter param) =>
			new KeyValuePair<string, IProtectionParameter>(param.Name, param);
	}
}
