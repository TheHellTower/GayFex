using System;
using dnlib.DotNet;

namespace Confuser.Optimizations {
	public interface IRegexTargetMethod : IEquatable<IRegexTargetMethod> {
		MethodDef Method { get; }
		MethodDef InstanceEquivalentMethod { get; }
		int PatternParameterIndex { get; }
		int OptionsParameterIndex { get; }
		int TimeoutParameterIndex { get; }
	}
}
