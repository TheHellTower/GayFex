using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal sealed class RegexCompilerResult {
		internal RegexCompileDef CompileDef { get; }
		internal TypeDef RunnerTypeDef { get; }
		internal TypeDef FactoryTypeDef { get; }
		internal TypeDef RegexTypeDef { get; }


		internal MethodDef CreateMethod { get; }
		internal MethodDef CreateWithTimeoutMethod { get; }

		internal IReadOnlyDictionary<IRegexTargetMethod, MethodDef> StaticHelperMethods { get; }

		internal RegexCompilerResult(RegexCompileDef compileDef,
			TypeDef runnerTypeDef, TypeDef factoryTypeDef, TypeDef regexTypeDef,
			MethodDef createMethod, MethodDef createWithTimeoutMethod,
			IReadOnlyDictionary<IRegexTargetMethod, MethodDef> staticHelperMethods) {
			CompileDef = compileDef;
			RunnerTypeDef = runnerTypeDef ?? throw new ArgumentNullException(nameof(runnerTypeDef));
			FactoryTypeDef = factoryTypeDef ?? throw new ArgumentNullException(nameof(factoryTypeDef));
			RegexTypeDef = regexTypeDef ?? throw new ArgumentNullException(nameof(regexTypeDef));
			CreateMethod = createMethod ?? throw new ArgumentNullException(nameof(createMethod));
			CreateWithTimeoutMethod = createWithTimeoutMethod;
			StaticHelperMethods = staticHelperMethods ?? throw new ArgumentNullException(nameof(staticHelperMethods));
		}
	}
}
