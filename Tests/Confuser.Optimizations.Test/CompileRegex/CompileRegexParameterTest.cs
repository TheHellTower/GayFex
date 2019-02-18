using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Confuser.Core;
using Xunit;

namespace Confuser.Optimizations.CompileRegex {
	public sealed class CompileRegexParameterTest {
		[Fact]
		[Trait("Category", "Optimization")]
		[Trait("Optimization", CompileRegexProtection.Id)]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void ParametersInDictionaryTest() {
			var parameters = new CompileRegexProtectionParameters();
			IReadOnlyDictionary<string, IProtectionParameter> paramDict = parameters;

			Assert.Contains(CreateEntry(parameters.I18NSafeMode), paramDict);
			Assert.Contains(CreateEntry(parameters.OnlyCompiled), paramDict);
			Assert.Contains(CreateEntry(parameters.SkipBrokenExpressions), paramDict);
		}

		private static KeyValuePair<string, IProtectionParameter> CreateEntry(IProtectionParameter param) =>
			new KeyValuePair<string, IProtectionParameter>(param.Name, param);
	}
}
