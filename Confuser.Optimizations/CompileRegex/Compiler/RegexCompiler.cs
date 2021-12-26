using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Confuser.Core;
using Confuser.Helpers;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpCode = System.Reflection.Emit.OpCode;
using OpCodes = dnlib.DotNet.Emit.OpCodes;
using RU = Confuser.Optimizations.CompileRegex.Compiler.ReflectionUtilities;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal sealed partial class RegexCompiler {
		private readonly IConfuserContext _context;
		private readonly ModuleDef _targetModule;
		private readonly RegexRunnerDef _regexRunnerDef;
		private readonly TypeDef _regexTypeDef;
		private readonly TypeDef _regexRunnerFactoryTypeDef;

		private int _compiledExpressions;

		private int _expectedExpressions = 100;

		private readonly RegexLWCGCompiler _realCompiler = new RegexLWCGCompiler();

		private string BaseName { get; } = "ConfuserCompiledRegex";

		internal int ExpectedExpressions {
			get => _expectedExpressions;
			set {
				if (value < 1)
					throw new ArgumentOutOfRangeException(nameof(value), value,
						"ExpectedExpressions is expected to be at least 1.");
				_expectedExpressions = value;
			}
		}

		internal RegexCompiler(IConfuserContext context, ModuleDef targetModule) {
			_context = context ?? throw new ArgumentNullException(nameof(context));
			_targetModule = targetModule ?? throw new ArgumentNullException(nameof(targetModule));

			var regexModule = targetModule
				.GetTypeRefs().First(tr => tr.FullName == CompileRegexProtection._RegexTypeFullName).ResolveTypeDefThrow()
				.Module;

			_regexRunnerDef = new RegexRunnerDef(regexModule);
			_regexTypeDef = regexModule.FindNormalThrow(CompileRegexProtection._RegexTypeFullName);
			_regexRunnerFactoryTypeDef =
				regexModule.FindNormalThrow(CompileRegexProtection._RegexNamespace + ".RegexRunnerFactory");
		}

		internal static bool IsCultureUnsafe(RegexCompileDef expression) {
			bool IsCultureInvariant(RegexOptions options) => (options & RegexOptions.CultureInvariant) != 0;
			bool IsIgnoreCase(RegexOptions options) => (options & RegexOptions.IgnoreCase) != 0;

			bool IsUnsafe(RegexOptions options) => !IsCultureInvariant(options) && IsIgnoreCase(options);

			if (IsUnsafe(expression.Options)) return true;
			RegexTree tree;
			try {
				tree = RegexParser.Parse(expression.Pattern, expression.Options, expression.Culture);
			}
			catch (ArgumentException) {
				return false;
			}

			if (tree.Root == null) return false;

			var uncheckedNodes = new Queue<RegexNode>();
			var alreadyProcessed = new HashSet<RegexNode>();
			uncheckedNodes.Enqueue(tree.Root);
			alreadyProcessed.Add(tree.Root);
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

			return false;
		}

		internal RegexCompilerResult Compile(RegexCompileDef expression) {
			RegexTree tree;
			RegexCode code;
			try {
				tree = RegexParser.Parse(expression.Pattern, expression.Options, expression.Culture);
				code = GetRegexCode(expression, tree);
			}
			catch (ArgumentException ex) {
				throw new RegexCompilerException(expression, ex);
			}

			var ic = CultureInfo.InvariantCulture;

			_compiledExpressions += 1;
			var baseName = string.Format(
				ic,
				BaseName + "{0:D" + ExpectedExpressions.ToString(ic).Length.ToString(ic) + "}",
				_compiledExpressions);

			var compiledRegexRunnerTypeDef = CompileRegexRunner(baseName, expression, code);
			var compiledRegexFactoryTypeDef = CompileRegexFactory(baseName, compiledRegexRunnerTypeDef);
			var compiledRegex = CompiledRegex(baseName, compiledRegexFactoryTypeDef, expression, code, tree);

			var staticHelperMethods = expression.TargetMethods.ToDictionary(
				staticMethod => staticMethod,
				staticMethod => CompileStaticAccessMethod(compiledRegex, staticMethod, expression));

			return new RegexCompilerResult(
				expression,
				compiledRegexRunnerTypeDef, compiledRegexFactoryTypeDef, compiledRegex.TypeDef,
				compiledRegex.FactoryMethod, compiledRegex.FactoryTimeoutMethod,
				staticHelperMethods
			);
		}

		private static RegexCode GetRegexCode(RegexCompileDef compile, RegexTree tree) {
			try {
				return RegexWriter.Write(tree);
			}
			catch (NotSupportedException) {
			}

			// The NotSupportedException is thrown in case the .NET Core 2.1 runtime is used.
			// Reflection on RegexWriter (ref struct) does not work in this case. So we'll need a workaround.
			var regexInstance = new Regex(compile.Pattern, compile.Options);
			var codeField = RU.GetInternalField(regexInstance.GetType(), "_code");
			return new RegexCode(codeField.GetValue(regexInstance));
		}

		#region CompileRegexRunner

		private TypeDef CompileRegexRunner(string baseName, RegexCompileDef expression, RegexCode code) {
			var runnerType = new TypeDefUser(baseName + "Runner",
				_targetModule.Import(_regexRunnerDef.RegexRunnerTypeDef)) {
				Visibility = TypeAttributes.NotPublic
			};
			_targetModule.AddAsNonNestedType(runnerType);

			GenerateDefaultConstructor(runnerType);

			var hasTimeout = expression.Timeout.HasValue && expression.Timeout.Value != Regex.InfiniteMatchTimeout;
			var factory = _realCompiler.FactoryInstanceFromCode(expression.Pattern, code, expression.Options, hasTimeout);

			var goMethod = ReadMethodDef(factory.GoMethod);
			var overrideGoMethod = CreateOverwriteMethodDef("Go", MethodSig.CreateInstance(_targetModule.CorLibTypes.Void),
				_regexRunnerDef.RegexRunnerTypeDef);
			overrideGoMethod.Body = goMethod.Body;
			overrideGoMethod.DeclaringType = runnerType;
			FixCheckTimeout(overrideGoMethod.Body);
			FixSystemSpanReference(overrideGoMethod.Body);
			CheckUnknownReferences(overrideGoMethod);

			var findFirstCharMethod = ReadMethodDef(factory.FindFirstCharMethod);
			var overrideFindFirstCharMethod = CreateOverwriteMethodDef("FindFirstChar",
				MethodSig.CreateInstance(_targetModule.CorLibTypes.Boolean), _regexRunnerDef.RegexRunnerTypeDef);
			overrideFindFirstCharMethod.Body = findFirstCharMethod.Body;
			overrideFindFirstCharMethod.DeclaringType = runnerType;
			FixSystemSpanReference(overrideFindFirstCharMethod.Body);
			CheckUnknownReferences(overrideFindFirstCharMethod);

			var overrideInitTrackCountMethod = CreateOverwriteMethodDef("InitTrackCount",
				MethodSig.CreateInstance(_targetModule.CorLibTypes.Void), _regexRunnerDef.RegexRunnerTypeDef);
			var initTrackCountMethod = factory.InitTrackCountMethod;
			if (!(initTrackCountMethod is null)) {
				var initTrackCountMethodDef = ReadMethodDef(initTrackCountMethod);
				overrideInitTrackCountMethod.Body = initTrackCountMethodDef.Body;
			}
			else {
				var importer = CreateImporter();
				overrideInitTrackCountMethod.Body = new CilBody();
				overrideInitTrackCountMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
				overrideInitTrackCountMethod.Body.Instructions.Add(Instruction.CreateLdcI4(code.TrackCount));
				overrideInitTrackCountMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Stfld, importer.Import(_regexRunnerDef.RunTrackCountFieldDef)));
				overrideInitTrackCountMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
			}
			overrideInitTrackCountMethod.DeclaringType = runnerType;
			CheckUnknownReferences(overrideInitTrackCountMethod);

			return runnerType;
		}

		private void FixCheckTimeout(CilBody body) {
			if (body is null) throw new ArgumentNullException(nameof(body));

			if (_regexRunnerDef.CheckTimeoutMethodDef != null) return;

			for (var i = body.Instructions.Count - 1; i >= 0; i--) {
				var instr = body.Instructions[i];
				if (instr.OpCode.Code != Code.Callvirt || !(instr.Operand is IMethodDefOrRef calledMethod) ||
					calledMethod.DeclaringType.FullName != _regexRunnerDef.RegexRunnerTypeDef.FullName ||
					calledMethod.Name != "CheckTimeout") continue;

				// Found the timeout method, but there is no check timeout method in the version of .NET we are using.
				body.RemoveInstruction(i);
				body.RemoveInstruction(i - 1);
				i++;
			}
		}

		private void FixSystemSpanReference(CilBody body) {
			if (body is null) throw new ArgumentNullException(nameof(body));

			// The changes applied by this method are only required in case System.Memory is not available.
			if (!(_targetModule.GetAssemblyRef("System.Memory") is null)) return;

			var runtimeService = _context.Registry.GetRequiredService<OptimizationsRuntimeService>();
			var runtimeModule = runtimeService.GetRuntimeModule();

			var stringViewTypeDef = runtimeModule.GetRuntimeType("Confuser.Optimizations.Runtime.ReadOnlyStringView", _targetModule);
			var stringHelperTypeDef = runtimeModule.GetRuntimeType("Confuser.Optimizations.Runtime.ReadOnlyStringHelper", _targetModule);

			var injectHelper = new InjectHelper(_context);

			foreach (var variable in body.Variables) {
				if (variable.Type.FullName.Equals("System.ReadOnlySpan`1<System.Char>")) {
					var injectResult = injectHelper.Inject(stringViewTypeDef, _targetModule, InjectBehaviors.RenameBehavior(_context));
					variable.Type = injectResult.Requested.Mapped.ToTypeSig();
				}
			}

			for (var i = 0; i < body.Instructions.Count; i++) {
				var instruction = body.Instructions[i];
				if (instruction.Operand is IMemberRef member) {
					if (member.DeclaringType.FullName.Equals("System.MemoryExtensions")) {
						if (member.Name.Equals("AsSpan")) {
							var constructorDef = stringHelperTypeDef.FindMethod("GetView");
							var injected = injectHelper.Inject(constructorDef, _targetModule, InjectBehaviors.RenameBehavior(_context));
							instruction.Operand = injected.Requested.Mapped;
						}
						else if (member.Name.Equals("IndexOf")) {
							var indexOfMethodDef = stringHelperTypeDef.FindMethod("IndexOf");
							var injected = injectHelper.Inject(indexOfMethodDef, _targetModule, InjectBehaviors.RenameBehavior(_context));
							instruction.Operand = injected.Requested.Mapped;
						}
						else if (member.Name.Equals("IndexOfAny")) {
							if (member is MethodSpec methodSpec && methodSpec.Method.GetParamCount() == 3) {
								var indexOfMethodDef = stringHelperTypeDef.FindMethod("IndexOfAny");
								var injected = injectHelper.Inject(indexOfMethodDef, _targetModule, InjectBehaviors.RenameBehavior(_context));
								instruction.Operand = injected.Requested.Mapped;
							}
							else
								throw new NotImplementedException();
						}
						else
							throw new NotImplementedException();
					}
					else if (member.DeclaringType.FullName.Equals("System.ReadOnlySpan`1<System.Char>")) {
						if (member.Name.Equals("get_Length")) {
							var getLengthMethodDef = stringViewTypeDef.FindMethod("get_Length");
							var injected = injectHelper.Inject(getLengthMethodDef, _targetModule, InjectBehaviors.RenameBehavior(_context));
							instruction.OpCode = OpCodes.Callvirt;
							instruction.Operand = injected.Requested.Mapped;
						}
						else if (member.Name.Equals("Slice")) {
							var getLengthMethodDef = stringViewTypeDef.FindMethod("Slice");
							var injected = injectHelper.Inject(getLengthMethodDef, _targetModule, InjectBehaviors.RenameBehavior(_context));
							instruction.OpCode = OpCodes.Callvirt;
							instruction.Operand = injected.Requested.Mapped;
						}
						else if (member.Name.Equals("get_Item")) {
							var getLengthMethodDef = stringViewTypeDef.FindMethod("get_Item");
							var injected = injectHelper.Inject(getLengthMethodDef, _targetModule, InjectBehaviors.RenameBehavior(_context));
							instruction.OpCode = OpCodes.Callvirt;
							instruction.Operand = injected.Requested.Mapped;
						}
						else
							throw new NotImplementedException();
					}
					else if (member.DeclaringType.FullName.Equals("System.Runtime.InteropServices.MemoryMarshal")) {
						if (member.Name.Equals("GetReference")) {
							var getLengthMethodDef = stringHelperTypeDef.FindMethod("GetReference");
							var injected = injectHelper.Inject(getLengthMethodDef, _targetModule, InjectBehaviors.RenameBehavior(_context));
							instruction.Operand = injected.Requested.Mapped;
						}
						else
							throw new NotImplementedException();
					}
				}
			}
		}

		private MethodDef ReadMethodDef(DynamicMethod method) {
			var importer = CreateImporter();
			var methodReader = new DynamicMethodBodyReader(_targetModule, method, importer);
			if (!methodReader.Read()) throw new Exception("Can't read compiled method.");

			var methodDef = methodReader.GetMethod();
			methodDef.Body.SimplifyBranches();
			methodDef.Body.SimplifyMacros(methodDef.Parameters);
			return methodDef;
		}

		private Importer CreateImporter() {
			var gpContext = new GenericParamContext();
			return new Importer(_targetModule, ImporterOptions.TryToUseDefs, gpContext, new Mapper(_context, _targetModule, _regexRunnerDef));
		}

		#endregion

		#region CompileRegexFactory

		private TypeDef CompileRegexFactory(string baseName, TypeDef runnerType) {
			var baseTypeRef = _targetModule.Import(_regexRunnerFactoryTypeDef);
			var runnerDef = _targetModule.Import(_regexRunnerDef.RegexRunnerTypeDef);
			var factoryType = new TypeDefUser(baseName + "Factory", baseTypeRef) {
				Visibility = TypeAttributes.NotPublic
			};
			_targetModule.AddAsNonNestedType(factoryType);

			var ctor = GenerateDefaultConstructor(factoryType);
			CheckUnknownReferences(ctor);

			var createInstanceSig = MethodSig.CreateInstance(runnerDef.ToTypeSig());
			var initTrackCountMethod =
				CreateOverwriteMethodDef("CreateInstance", createInstanceSig, _regexRunnerFactoryTypeDef);
			initTrackCountMethod.DeclaringType = factoryType;

			var compiler = new RegexMethodCompiler(_targetModule, initTrackCountMethod);

			compiler.Newobj(runnerType.FindDefaultConstructor());
			compiler.Ret();

			CheckUnknownReferences(initTrackCountMethod);

			return factoryType;
		}

		#endregion

		private MethodDefUser CreateOverwriteMethodDef(string name, MethodSig methodSig, TypeDef baseType) {
			var baseMethod = baseType.FindMethod(name, methodSig);

			var newMethodDef = new MethodDefUser(name, methodSig) {
				Attributes = (baseMethod.Attributes & ~(MethodAttributes.NewSlot | MethodAttributes.Abstract)) |
							 MethodAttributes.Final
			};

			newMethodDef.Overrides.Add(new MethodOverride(newMethodDef, _targetModule.Import(baseMethod)));
			return newMethodDef;
		}

		private (TypeDef TypeDef, MethodDef FactoryMethod, MethodDef FactoryTimeoutMethod) CompiledRegex(
			string baseName, TypeDef factoryType, RegexCompileDef expression, RegexCode code, RegexTree tree) {
			var baseTypeRef = _targetModule.Import(_regexTypeDef);
			var regexType = new TypeDefUser(baseName, baseTypeRef) {
				Visibility = TypeAttributes.NotPublic
			};
			_targetModule.AddAsNonNestedType(regexType);

			var defaultCtor = CreateConstructorDef();
			defaultCtor.DeclaringType = regexType;

			var compiler = new RegexConstructorCompiler(_targetModule, defaultCtor);
			compiler.GenerateDefaultConstructor(factoryType, expression, code, tree);
			CheckUnknownReferences(defaultCtor);

			var factoryMethodDef = new MethodDefUser("GetRegex", MethodSig.CreateStatic(baseTypeRef.ToTypeSig())) {
				Attributes = MethodAttributes.Assembly | MethodAttributes.Static,
				DeclaringType = regexType
			};
			var body = factoryMethodDef.Body = new CilBody();
			body.Instructions.Add(OpCodes.Newobj.ToInstruction(defaultCtor));
			body.Instructions.Add(OpCodes.Ret.ToInstruction());
			CheckUnknownReferences(factoryMethodDef);

			if (expression.StaticTimeout) return (regexType, factoryMethodDef, null);

			var typeRefFinder = new TypeRefFinder(_targetModule);
			var timespanTypeRef = typeRefFinder.FindType("System.TimeSpan");
			if (timespanTypeRef is TypeDef timespanTypeDef)
				timespanTypeRef = _targetModule.Import(timespanTypeDef);

			var timespanTypeSig = timespanTypeRef.ToTypeSig();
			var timeSpanCtor = CreateConstructorDef(timespanTypeSig);
			timeSpanCtor.DeclaringType = regexType;

			compiler = new RegexConstructorCompiler(_targetModule, timeSpanCtor);
			compiler.GenerateTimeoutConstructor(defaultCtor);
			CheckUnknownReferences(timeSpanCtor);

			var timeoutFactoryMethodDef = new MethodDefUser("GetRegex",
				MethodSig.CreateStatic(baseTypeRef.ToTypeSig(), timespanTypeSig)) {
				Attributes = MethodAttributes.Assembly | MethodAttributes.Static,
				DeclaringType = regexType
			};
			body = timeoutFactoryMethodDef.Body = new CilBody();
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Newobj.ToInstruction(timeSpanCtor));
			body.Instructions.Add(OpCodes.Ret.ToInstruction());
			CheckUnknownReferences(timeoutFactoryMethodDef);

			return (regexType, factoryMethodDef, timeoutFactoryMethodDef);
		}

		private MethodDef CompileStaticAccessMethod(
			(TypeDef TypeDef, MethodDef FactoryMethod, MethodDef FactoryTimeoutMethod) compiledRegex,
			IRegexTargetMethod targetMethod, RegexCompileDef compileDef) =>
			CompileStaticAccessMethod(compiledRegex.TypeDef, compiledRegex.FactoryMethod,
				compiledRegex.FactoryTimeoutMethod, targetMethod, compileDef);

		private MethodDef CompileStaticAccessMethod(TypeDef compiledRegexType, MethodDef factoryMethod,
			MethodDef factoryTimeoutMethod, IRegexTargetMethod targetMethod, RegexCompileDef compileDef) {
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
			else
				body.Instructions.Add(OpCodes.Call.ToInstruction(factoryMethod));

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

		private MethodDef GenerateDefaultConstructor(TypeDef runnerType) {
			Debug.Assert(runnerType != null, $"{nameof(runnerType)} != null");

			var body = new CilBody();

			var baseCtor = runnerType.BaseType.ResolveTypeDefThrow().FindDefaultConstructor();
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Call.ToInstruction(_targetModule.Import(baseCtor)));
			body.Instructions.Add(OpCodes.Ret.ToInstruction());

			var defaultCtor = CreateConstructorDef();
			defaultCtor.DeclaringType = runnerType;
			defaultCtor.Body = body;

			return defaultCtor;
		}

		private MethodDefUser CreateConstructorDef() {
			const MethodImplAttributes implFlags = MethodImplAttributes.IL | MethodImplAttributes.Managed;
			const MethodAttributes flags = MethodAttributes.Assembly |
										   MethodAttributes.HideBySig | MethodAttributes.ReuseSlot |
										   MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;

			return new MethodDefUser(".ctor",
				MethodSig.CreateInstance(_targetModule.CorLibTypes.Void), implFlags, flags);
		}

		private MethodDefUser CreateConstructorDef(TypeSig param) {
			const MethodImplAttributes implFlags = MethodImplAttributes.IL | MethodImplAttributes.Managed;
			const MethodAttributes flags = MethodAttributes.Assembly |
										   MethodAttributes.HideBySig | MethodAttributes.ReuseSlot |
										   MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;

			return new MethodDefUser(".ctor",
				MethodSig.CreateInstance(_targetModule.CorLibTypes.Void, param), implFlags, flags);
		}

		private void CheckUnknownReferences(MethodDef methodDef) {
			bool IsKnownAssembly(IAssembly assemblyRef) {
				var comp = AssemblyNameComparer.CompareAll;
				return comp.Equals(_targetModule.Assembly, assemblyRef)
					|| _targetModule.GetAssemblyRefs().Any(knownAssemblyRef => comp.Equals(assemblyRef, knownAssemblyRef));
			}

			void CheckAndReportAssembly(IAssembly assemblyRef) {
				if (IsKnownAssembly(assemblyRef)) return;

				var logger = _context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RegexCompiler));
				logger.LogError("Detected corruption caused by compile regex optimization. The assembly will be unusable.");
				throw new ConfuserException();
			}

			foreach (var localVariable in methodDef.Body.Variables)
				CheckAndReportAssembly(localVariable.Type.DefinitionAssembly);

			foreach (var operand in methodDef.Body.Instructions.Select(i => i.Operand).Where(op => !(op is null)))
				switch (operand) {
					case ITypeDefOrRef typeRef:
						CheckAndReportAssembly(typeRef.DefinitionAssembly);
						break;
					case IMemberRef memberRef:
						CheckAndReportAssembly(memberRef.DeclaringType.DefinitionAssembly);
						break;
				}
		}
	}
}
