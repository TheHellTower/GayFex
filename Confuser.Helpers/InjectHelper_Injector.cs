using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Helpers {
	public static partial class InjectHelper {
		/// <summary>
		///     The injector actually does the injecting.
		/// </summary>
		private sealed class Injector : ImportResolver {

			private readonly Dictionary<IMemberDef, IMemberDef> _injectedMembers;

			private InjectContext InjectContext { get; }

			private IInjectBehavior InjectBehavior { get; }

			internal IReadOnlyDictionary<IMemberDef, IMemberDef> InjectedMembers => _injectedMembers;

			private Queue<IMemberDef> PendingForInject { get; }

			internal Injector(InjectContext injectContext, IInjectBehavior injectBehavior) {
				InjectContext = injectContext ?? throw new ArgumentNullException(nameof(injectContext));
				InjectBehavior = injectBehavior ?? throw new ArgumentNullException(nameof(injectBehavior));
				PendingForInject = new Queue<IMemberDef>();
				_injectedMembers = new Dictionary<IMemberDef, IMemberDef>();
			}

			private TypeDefUser CopyDef(TypeDef source) {
				if (source == null) throw new ArgumentNullException(nameof(source));

				if (_injectedMembers.TryGetValue(source, out var importedMember))
					return (TypeDefUser)importedMember;

				var typeDefUser = new TypeDefUser(source.Namespace, source.Name) {
					Attributes = source.Attributes
				};

				if (source.HasClassLayout)
					typeDefUser.ClassLayout = new ClassLayoutUser(source.ClassLayout.PackingSize, source.ClassSize);

				CloneGenericParameters(source, typeDefUser);

				_injectedMembers.Add(source, typeDefUser);
				PendingForInject.Enqueue(source);

				return typeDefUser;
			}

			private MethodDefUser CopyDef(MethodDef source) {
				if (source == null) throw new ArgumentNullException(nameof(source));

				if (_injectedMembers.TryGetValue(source, out var importedMember))
					return (MethodDefUser)importedMember;

				var methodDefUser = new MethodDefUser(source.Name, null, source.ImplAttributes, source.Attributes) {
					Attributes = source.Attributes
				};

				CloneGenericParameters(source, methodDefUser);

				_injectedMembers.Add(source, methodDefUser);
				PendingForInject.Enqueue(source);

				return methodDefUser;
			}

			internal FieldDefUser CopyDef(FieldDef source) {
				if (source == null) throw new ArgumentNullException(nameof(source));

				var fieldDefUser = new FieldDefUser(source.Name, null, source.Attributes) {
					Attributes = source.Attributes
				};

				if (_injectedMembers.TryGetValue(source, out var importedMember))
					return (FieldDefUser)importedMember;

				_injectedMembers.Add(source, fieldDefUser);
				PendingForInject.Enqueue(source);

				return fieldDefUser;
			}

			private static void CloneGenericParameters(ITypeOrMethodDef origin, ITypeOrMethodDef result) {
				if (origin.HasGenericParameters)
					foreach (var genericParam in origin.GenericParameters)
						result.GenericParameters.Add(new GenericParamUser(genericParam.Number, genericParam.Flags, "-"));
			}

			private IReadOnlyCollection<IMemberDef> InjectRemaining(Importer importer) {
				var resultBuilder = ImmutableList.CreateBuilder<IMemberDef>();

				while (PendingForInject.Count > 0) {
					var memberDef = PendingForInject.Dequeue();
					if (memberDef is TypeDef typeDef)
						resultBuilder.Add(InjectTypeDef(typeDef, importer));
					else if (memberDef is MethodDef methodDef)
						resultBuilder.Add(InjectMethodDef(methodDef, importer));
					else if (memberDef is FieldDef fieldDef)
						resultBuilder.Add(InjectFieldDef(fieldDef, importer));
				}
				return resultBuilder.ToImmutable();
			}

			internal MethodDef Inject(MethodDef methodDef) {
				var existingMappedMethodDef = InjectContext.ResolveMapped(methodDef);
				if (existingMappedMethodDef != null) return existingMappedMethodDef;

				var importer = new Importer(InjectContext.TargetModule) { Resolver = this };
				var result = InjectMethodDef(methodDef, importer);
				InjectRemaining(importer);
				return result;
			}

			private TypeDef InjectTypeDef(TypeDef typeDef, Importer importer) {
				if (typeDef == null) throw new ArgumentNullException(nameof(typeDef));

				var existingTypeDef = InjectContext.ResolveMapped(typeDef);
				if (existingTypeDef != null) return existingTypeDef;

				var newTypeDef = CopyDef(typeDef);
				newTypeDef.BaseType = (ITypeDefOrRef)importer.Import(typeDef.BaseType);

				if (typeDef.DeclaringType != null)
					newTypeDef.DeclaringType = InjectTypeDef(typeDef.DeclaringType, importer);

				foreach (var iface in typeDef.Interfaces)
					newTypeDef.Interfaces.Add(InjectInterfaceImpl(iface, importer));
				
				InjectBehavior.Process(typeDef, newTypeDef);

				if (!newTypeDef.IsNested)
					InjectContext.TargetModule.Types.Add(newTypeDef);
				
				InjectContext.TargetModule.UpdateRowId(newTypeDef);
				InjectContext.ApplyMapping(typeDef, newTypeDef);

				return newTypeDef;
			}

			private InterfaceImplUser InjectInterfaceImpl(InterfaceImpl interfaceImpl, Importer importer) {
				if (interfaceImpl == null) throw new ArgumentNullException(nameof(interfaceImpl));

				var typeDefOrRef = (ITypeDefOrRef)importer.Import(interfaceImpl.Interface);
				if (!(typeDefOrRef is TypeDef typeDef))
					typeDef = ((TypeRef)typeDefOrRef).Resolve();
				if (typeDef != null && !typeDef.IsInterface)
					throw new InvalidOperationException("Type for Interface is not a interface?!");

				var resultImpl = new InterfaceImplUser(typeDefOrRef);
				InjectContext.TargetModule.UpdateRowId(resultImpl);
				return resultImpl;
			}

			private FieldDef InjectFieldDef(FieldDef fieldDef, Importer importer) {
				if (fieldDef == null) throw new ArgumentNullException(nameof(fieldDef));

				var existingFieldDef = InjectContext.ResolveMapped(fieldDef);
				if (existingFieldDef != null) return existingFieldDef;

				var newFieldDef = CopyDef(fieldDef);
				newFieldDef.Signature = importer.Import(fieldDef.Signature);
				newFieldDef.DeclaringType = (TypeDef)importer.Import(fieldDef.DeclaringType);

				InjectBehavior.Process(fieldDef, newFieldDef);
				InjectContext.TargetModule.UpdateRowId(newFieldDef);
				InjectContext.ApplyMapping(fieldDef, newFieldDef);

				return newFieldDef;
			}

			private MethodDef InjectMethodDef(MethodDef methodDef, Importer importer) {
				if (methodDef == null) throw new ArgumentNullException(nameof(methodDef));

				var existingMethodDef = InjectContext.ResolveMapped(methodDef);
				if (existingMethodDef != null) return existingMethodDef;

				var newMethodDef = CopyDef(methodDef);
				newMethodDef.DeclaringType = (TypeDef)importer.Import(methodDef.DeclaringType);
				newMethodDef.Signature = importer.Import(methodDef.Signature);
				newMethodDef.Parameters.UpdateParameterTypes();

				if (methodDef.ImplMap != null)
					newMethodDef.ImplMap = new ImplMapUser(new ModuleRefUser(InjectContext.TargetModule, methodDef.ImplMap.Module.Name), methodDef.ImplMap.Name, methodDef.ImplMap.Attributes);

				foreach (var ca in methodDef.CustomAttributes)
					newMethodDef.CustomAttributes.Add(new CustomAttribute((ICustomAttributeType)importer.Import(ca.Constructor)));

				if (methodDef.HasBody) {
					methodDef.Body.SimplifyBranches();
					methodDef.Body.SimplifyMacros(methodDef.Parameters);

					newMethodDef.Body = new CilBody(methodDef.Body.InitLocals, new List<Instruction>(), new List<ExceptionHandler>(), new List<Local>());
					newMethodDef.Body.MaxStack = methodDef.Body.MaxStack;

					var bodyMap = new Dictionary<object, object>();

					foreach (var local in methodDef.Body.Variables) {
						var newLocal = new Local(importer.Import(local.Type));
						newMethodDef.Body.Variables.Add(newLocal);
						newLocal.Name = local.Name;

						bodyMap[local] = newLocal;
					}

					foreach (var instr in methodDef.Body.Instructions) {
						var newInstr = new Instruction(instr.OpCode, instr.Operand) {
							SequencePoint = instr.SequencePoint
						};

						if (newInstr.Operand is IType typeOp)
							newInstr.Operand = importer.Import(typeOp);

						else if (newInstr.Operand is IMethod methodOp)
							newInstr.Operand = importer.Import(methodOp);

						else if (newInstr.Operand is IField fieldOp)
							newInstr.Operand = importer.Import(fieldOp);

						newMethodDef.Body.Instructions.Add(newInstr);
						bodyMap[instr] = newInstr;
					}

					foreach (var instr in newMethodDef.Body.Instructions) {
						if (instr.Operand != null && bodyMap.ContainsKey(instr.Operand))
							instr.Operand = bodyMap[instr.Operand];

						else if (instr.Operand is Instruction[] instructionArrayOp)
							instr.Operand = (instructionArrayOp).Select(target => (Instruction)bodyMap[target]).ToArray();
					}

					foreach (var eh in methodDef.Body.ExceptionHandlers)
						newMethodDef.Body.ExceptionHandlers.Add(new ExceptionHandler(eh.HandlerType) {
							CatchType = eh.CatchType == null ? null : (ITypeDefOrRef)importer.Import(eh.CatchType),
							TryStart = (Instruction)bodyMap[eh.TryStart],
							TryEnd = (Instruction)bodyMap[eh.TryEnd],
							HandlerStart = (Instruction)bodyMap[eh.HandlerStart],
							HandlerEnd = (Instruction)bodyMap[eh.HandlerEnd],
							FilterStart = eh.FilterStart == null ? null : (Instruction)bodyMap[eh.FilterStart]
						});

					newMethodDef.Body.OptimizeMacros();
					newMethodDef.Body.OptimizeBranches();
				}

				InjectBehavior.Process(methodDef, newMethodDef);
				InjectContext.TargetModule.UpdateRowId(newMethodDef);
				InjectContext.ApplyMapping(methodDef, newMethodDef);

				return newMethodDef;
			}

			#region ImportResolver

			public override TypeDef Resolve(TypeDef typeDef) {
				var mappedType = InjectContext.ResolveMapped(typeDef);
				if (mappedType != null) return mappedType;

				if (typeDef.Module == InjectContext.OriginModule)
					return CopyDef(typeDef);
				return base.Resolve(typeDef);
			}

			public override MethodDef Resolve(MethodDef methodDef) {
				var mappedMethod = InjectContext.ResolveMapped(methodDef);
				if (mappedMethod != null) return mappedMethod;

				if (methodDef.Module == InjectContext.OriginModule)
					return CopyDef(methodDef);
				return base.Resolve(methodDef);
			}

			public override FieldDef Resolve(FieldDef fieldDef) {
				var mappedField = InjectContext.ResolveMapped(fieldDef);
				if (mappedField != null) return mappedField;

				if (fieldDef.Module == InjectContext.OriginModule)
					return CopyDef(fieldDef);
				return base.Resolve(fieldDef);
			}

			#endregion
		}
	}
}
