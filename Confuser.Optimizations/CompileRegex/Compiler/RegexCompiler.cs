using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal sealed class RegexCompiler {
		private readonly ModuleDef _targetModule;
		private readonly RegexRunnerDef _regexRunnerDef;
		private readonly TypeDef _regexTypeDef;
		private readonly TypeDef _regexRunnerFactoryTypeDef;

		private int _compiledExpressions = 0;

		private int _expectedExpressions = 100;
		private string _baseName = "ConfuserCompiledRegex";

		internal string Namespace { get; set; }
		internal string BaseName {
			get => _baseName;
			set {
				if (value == null) throw new ArgumentNullException(nameof(value));
				if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("The base name must not be empty.", nameof(value));
				_baseName = value;
			}
		}

		internal int ExpectedExpressions {
			get => _expectedExpressions;
			set {
				if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), value, "ExpectedExpressions is expected to be at least 1.");
				_expectedExpressions = value;
			}
		}

		internal RegexCompiler(ModuleDef targetModule) {
			_targetModule = targetModule ?? throw new ArgumentNullException(nameof(targetModule));

			var regexModule = targetModule.GetTypeRefs().Where(tr => tr.FullName == CompileRegexProtection._RegexTypeFullName).First().ResolveTypeDefThrow().Module;

			_regexRunnerDef = new RegexRunnerDef(regexModule);
			_regexTypeDef = regexModule.FindNormalThrow(CompileRegexProtection._RegexTypeFullName);
			_regexRunnerFactoryTypeDef = regexModule.FindNormalThrow(CompileRegexProtection._RegexNamespace + ".RegexRunnerFactory");
		}

		internal static bool IsCultureUnsafe(RegexCompileDef expression) {
			bool IsCultureInvariant(RegexOptions options) => (options & RegexOptions.CultureInvariant) != 0;
			bool IsIgnoreCase(RegexOptions options) => (options & RegexOptions.IgnoreCase) != 0;

			bool IsUnsafe(RegexOptions options) => !IsCultureInvariant(options) && IsIgnoreCase(options);

			if (IsUnsafe(expression.Options)) return true;
			RegexTree tree;
			try {
				tree = RegexParser.Parse(expression.Pattern, expression.Options);
			}
			catch (ArgumentException) {
				return false;
			}

			if (tree._root != null) {
				var uncheckedNodes = new Queue<RegexNode>();
				var alreadyProcessed = new HashSet<RegexNode>();
				uncheckedNodes.Enqueue(tree._root);
				alreadyProcessed.Add(tree._root);
				while (uncheckedNodes.Any()) {
					var currentNode = uncheckedNodes.Dequeue();
					if (IsUnsafe(currentNode._options)) return true;

					var children = currentNode._children;
					if (children != null)
						foreach (var child in children)
							if (alreadyProcessed.Add(child))
								uncheckedNodes.Enqueue(child);

					var nextNode = currentNode._next;
					if (nextNode != null && alreadyProcessed.Add(nextNode))
						uncheckedNodes.Enqueue(nextNode);
				}
			}

			return false;
		}

		internal RegexCompilerResult Compile(RegexCompileDef expression) {
			RegexTree tree;
			RegexCode code;
			try {
				tree = RegexParser.Parse(expression.Pattern, expression.Options);
				code = RegexWriter.Write(tree);
			} catch (ArgumentException ex) {
				throw new RegexCompilerException(expression, ex);
			}

			_compiledExpressions += 1;
			var baseName = string.Format(BaseName + "{0:D" + ExpectedExpressions.ToString().Length.ToString() + "}", _compiledExpressions);

			var compiledRegexRunnerTypeDef = CompileRegexRunner(baseName, expression, code);
			var compiledRegexFactoryTypeDef = CompileRegexFactory(baseName, compiledRegexRunnerTypeDef);
			var compiledRegex = CompiledRegex(baseName, compiledRegexFactoryTypeDef, expression, code, tree);

			var staticHelperMethods = new Dictionary<IRegexTargetMethod, MethodDef>();
			foreach (var staticMethod in expression.TargetMethods) {
				staticHelperMethods.Add(
					staticMethod,
					CompileStaticAccessMethod(compiledRegex, staticMethod, expression));
			}

			return new RegexCompilerResult(
				expression,
				compiledRegexRunnerTypeDef, compiledRegexFactoryTypeDef, compiledRegex.TypeDef,
				compiledRegex.FactoryMethod, compiledRegex.FactoryTimeoutMethod,
				staticHelperMethods
			);
		}

		#region CompileRegexRunner
		private TypeDef CompileRegexRunner(string baseName, RegexCompileDef expression, RegexCode code) {
			var runnerType = new TypeDefUser(Namespace, baseName + "Runner", _targetModule.Import(_regexRunnerDef.RegexRunnerTypeDef)) {
				Visibility = TypeAttributes.NotPublic
			};
			_targetModule.AddAsNonNestedType(runnerType);

			GenerateDefaultConstructor(runnerType);

			var goMethod = CreateOverwriteMethodDef("Go", MethodSig.CreateInstance(_targetModule.CorLibTypes.Void), _regexRunnerDef.RegexRunnerTypeDef);
			goMethod.DeclaringType = runnerType;
			GenerateGo(expression, goMethod, code);

			var findFirstCharMethod = CreateOverwriteMethodDef("FindFirstChar", MethodSig.CreateInstance(_targetModule.CorLibTypes.Boolean), _regexRunnerDef.RegexRunnerTypeDef);
			findFirstCharMethod.DeclaringType = runnerType;
			GenerateFindFirstChar(expression.Options, findFirstCharMethod, code);

			var initTrackCountMethod = CreateOverwriteMethodDef("InitTrackCount", MethodSig.CreateInstance(_targetModule.CorLibTypes.Void), _regexRunnerDef.RegexRunnerTypeDef);
			initTrackCountMethod.DeclaringType = runnerType;
			GenerateInitTrackCount(initTrackCountMethod, code);

			return runnerType;
		}

		private void GenerateGo(RegexCompileDef expression, MethodDef goMethod, RegexCode code) {
			var compiler = new RegexRunnerGoMethodCompiler(_targetModule, goMethod, _regexRunnerDef) {
				CheckTimeout = expression.Timeout.HasValue || !expression.StaticTimeout
			};
			compiler.GenerateGo(expression.Options, code);
		}

		private void GenerateFindFirstChar(RegexOptions options, MethodDef findFirstCharMethod, RegexCode code) {
			var compiler = new RegexRunnerFindFirstCharMethodCompiler(_targetModule, findFirstCharMethod, _regexRunnerDef);
			compiler.GenerateFindFirstChar(options, code);
		}

		private void GenerateInitTrackCount(MethodDef findFirstCharMethod, RegexCode code) {
			var compiler = new RegexRunnerMethodCompiler(_targetModule, findFirstCharMethod, _regexRunnerDef) {
				CacheRegexRunnerFieldsInLocalVariables = false
			};

			compiler.StRunnerField(_regexRunnerDef.runtrackcountFieldDef, () => {
				compiler.Ldc(code._trackcount);
			});
			compiler.Ret();

			compiler.FinishMethod();
		}
		#endregion

		#region CompileRegexFactory
		private TypeDef CompileRegexFactory(string baseName, TypeDef runnerType) {
			var baseTypeRef = _targetModule.Import(_regexRunnerFactoryTypeDef);
			var runnerDef = _targetModule.Import(_regexRunnerDef.RegexRunnerTypeDef);
			var factoryType = new TypeDefUser(Namespace, baseName + "Factory", baseTypeRef) {
				Visibility = TypeAttributes.NotPublic
			};
			_targetModule.AddAsNonNestedType(factoryType);

			GenerateDefaultConstructor(factoryType);

			var createInstanceSig = MethodSig.CreateInstance(runnerDef.ToTypeSig());
			var initTrackCountMethod = CreateOverwriteMethodDef("CreateInstance", createInstanceSig, _regexRunnerFactoryTypeDef);
			initTrackCountMethod.DeclaringType = factoryType;

			var compiler = new RegexMethodCompiler(_targetModule, initTrackCountMethod);

			compiler.Newobj(runnerType.FindDefaultConstructor());
			compiler.Ret();
			compiler.FinishMethod();

			return factoryType;
		}
		#endregion

		private MethodDefUser CreateOverwriteMethodDef(string name, MethodSig methodSig, TypeDef baseType) {
			var baseMethod = baseType.FindMethod(name, methodSig);

			var newMethodDef = new MethodDefUser(name, methodSig) {
				Attributes = (baseMethod.Attributes & ~(MethodAttributes.NewSlot | MethodAttributes.Abstract)) | MethodAttributes.Final,
			};

			newMethodDef.Overrides.Add(new MethodOverride(newMethodDef, _targetModule.Import(baseMethod)));
			return newMethodDef;
		}

		private (TypeDef TypeDef, MethodDef FactoryMethod, MethodDef FactoryTimeoutMethod) CompiledRegex(string baseName, TypeDef factoryType, RegexCompileDef expression, RegexCode code, RegexTree tree) {
			var baseTypeRef = _targetModule.Import(_regexTypeDef);
			var regexType = new TypeDefUser(Namespace, baseName, baseTypeRef) {
				Visibility = TypeAttributes.NotPublic
			};
			_targetModule.AddAsNonNestedType(regexType);

			var defaultCtor = CreateConstructorDef();
			defaultCtor.DeclaringType = regexType;

			var compiler = new RegexConstructorCompiler(_targetModule, defaultCtor);
			compiler.GenerateDefaultConstructor(factoryType, expression, code, tree);

			var factoryMethodDef = new MethodDefUser("GetRegex", MethodSig.CreateStatic(baseTypeRef.ToTypeSig())) {
				Attributes = MethodAttributes.Assembly | MethodAttributes.Static,
				DeclaringType = regexType
			};
			var body = factoryMethodDef.Body = new CilBody();
			body.Instructions.Add(OpCodes.Newobj.ToInstruction(defaultCtor));
			body.Instructions.Add(OpCodes.Ret.ToInstruction());

			MethodDefUser timeoutFactoryMethodDef = null;
			if (!expression.StaticTimeout) {
				var typeRefFinder = new TypeRefFinder(_targetModule);
				var timespanTypeRef = typeRefFinder.FindType("System.TimeSpan");
				if (timespanTypeRef is TypeDef timespanTypeDef) {
					timespanTypeRef = _targetModule.Import(timespanTypeDef);
				}
				var timespanTypeSig = timespanTypeRef.ToTypeSig();
				var timeSpanCtor = CreateConstructorDef(timespanTypeSig);
				timeSpanCtor.DeclaringType = regexType;

				compiler = new RegexConstructorCompiler(_targetModule, timeSpanCtor);
				compiler.GenerateTimeoutConstructor(defaultCtor);

				timeoutFactoryMethodDef = new MethodDefUser("GetRegex",
					MethodSig.CreateStatic(baseTypeRef.ToTypeSig(), timespanTypeSig)) {
					Attributes = MethodAttributes.Assembly | MethodAttributes.Static,
					DeclaringType = regexType
				};
				body = timeoutFactoryMethodDef.Body = new CilBody();
				body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
				body.Instructions.Add(OpCodes.Newobj.ToInstruction(timeSpanCtor));
				body.Instructions.Add(OpCodes.Ret.ToInstruction());
			}
			return (regexType, factoryMethodDef, timeoutFactoryMethodDef);
		}

		private MethodDef CompileStaticAccessMethod((TypeDef TypeDef, MethodDef FactoryMethod, MethodDef FactoryTimeoutMethod) compiledRegex, IRegexTargetMethod targetMethod, RegexCompileDef compileDef) =>
			CompileStaticAccessMethod(compiledRegex.TypeDef, compiledRegex.FactoryMethod, compiledRegex.FactoryTimeoutMethod, targetMethod, compileDef);

		private MethodDef CompileStaticAccessMethod(TypeDef compiledRegexType, MethodDef factoryMethod, MethodDef factoryTimeoutMethod, IRegexTargetMethod targetMethod, RegexCompileDef compileDef) {
			// Create some utility method that replace the default static methods.

			if (targetMethod.Method.IsConstructor) return null;

			var importer = new Importer(compiledRegexType.Module, ImporterOptions.TryToUseDefs);

			var methodSig = MethodSig.CreateStatic(importer.Import(targetMethod.Method.ReturnType));
			var timeoutIndex = -1;
			for (var i = 0; i < targetMethod.Method.MethodSig.Params.Count; i++) {
				if (i == targetMethod.PatternParameterIndex) continue;
				if (i == targetMethod.OptionsParameterIndex) continue;
				if (compileDef.StaticTimeout && i == targetMethod.TimeoutParameterIndex) continue;

				methodSig.Params.Add(importer.Import(targetMethod.Method.MethodSig.Params[i]));
				if (i == targetMethod.TimeoutParameterIndex)
					timeoutIndex = methodSig.Params.Count - 1;
			}

			var staticMethodDef = new MethodDefUser(targetMethod.Method.Name, methodSig) {
				Attributes = MethodAttributes.Assembly | MethodAttributes.Static,
				DeclaringType = compiledRegexType
			};

			staticMethodDef.Parameters.UpdateParameterTypes();

			var body = staticMethodDef.Body = new CilBody();

			if (!compileDef.StaticTimeout && targetMethod.TimeoutParameterIndex >= 0) {
				Debug.Assert(timeoutIndex != -1);
				body.Instructions.Add(OpCodes.Ldarg.ToInstruction(staticMethodDef.Parameters[timeoutIndex]));
				body.Instructions.Add(OpCodes.Call.ToInstruction(factoryTimeoutMethod));
			}
			else {
				body.Instructions.Add(OpCodes.Call.ToInstruction(factoryMethod));
			}

			foreach (var param in staticMethodDef.Parameters) {
				if (param.MethodSigIndex == timeoutIndex) continue;
				body.Instructions.Add(OpCodes.Ldarg.ToInstruction(param));
			}
			body.Instructions.Add(
				OpCodes.Callvirt.ToInstruction(
					importer.Import(targetMethod.InstanceEquivalentMethod)));
			body.Instructions.Add(OpCodes.Ret.ToInstruction());

			return staticMethodDef;
		}

		private void GenerateDefaultConstructor(TypeDef runnerType) {
			Debug.Assert(runnerType != null, $"{nameof(runnerType)} != null");

			var body = new CilBody();

			var baseCtor = runnerType.BaseType.ResolveTypeDefThrow().FindDefaultConstructor();
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Call.ToInstruction(_targetModule.Import(baseCtor)));
			body.Instructions.Add(OpCodes.Ret.ToInstruction());

			var defaultCtor = CreateConstructorDef();
			defaultCtor.DeclaringType = runnerType;
			defaultCtor.Body = body;
		}

		private MethodDefUser CreateConstructorDef() {
			var implFlags = MethodImplAttributes.IL | MethodImplAttributes.Managed;
			var flags = MethodAttributes.Assembly |
						MethodAttributes.HideBySig | MethodAttributes.ReuseSlot |
						MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;

			return new MethodDefUser(".ctor",
				MethodSig.CreateInstance(_targetModule.CorLibTypes.Void), implFlags, flags);
		}

		private MethodDefUser CreateConstructorDef(TypeSig param) {
			var implFlags = MethodImplAttributes.IL | MethodImplAttributes.Managed;
			var flags = MethodAttributes.Assembly |
						MethodAttributes.HideBySig | MethodAttributes.ReuseSlot |
						MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;

			return new MethodDefUser(".ctor",
				MethodSig.CreateInstance(_targetModule.CorLibTypes.Void, param), implFlags, flags);
		}
	}
}
