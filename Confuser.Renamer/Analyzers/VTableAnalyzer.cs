using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Confuser.Core;
using Confuser.Renamer.References;
using dnlib.DotNet;

namespace Confuser.Renamer.Analyzers {
	public class VTableAnalyzer : IRenamer {
		void IRenamer.Analyze(ConfuserContext context, INameService service, ProtectionParameters parameters, IDnlibDef def) {
			switch (def) {
				case TypeDef typeDef:
					Analyze(service, context.Modules, typeDef);
					break;
				case MethodDef methodDef:
					Analyze(service, context.Modules, methodDef);
					break;
			}
		}

		public static void Analyze(INameService service, ICollection<ModuleDefMD> modules, TypeDef type) {
			if (type.IsInterface)
				return;

			var vTbl = service.GetVTables()[type];
			foreach (var ifaceVTbl in vTbl.InterfaceSlots.Values) {
				foreach (var slot in ifaceVTbl) {
					if (slot.Overrides == null)
						continue;
					Debug.Assert(slot.Overrides.MethodDef.DeclaringType.IsInterface);
					// A method in base type can implements an interface method for a
					// derived type. If the base type/interface is not in our control, we should
					// not rename the methods.
					bool baseUnderCtrl = modules.Contains(slot.MethodDef.DeclaringType.Module as ModuleDefMD);
					bool ifaceUnderCtrl = modules.Contains(slot.Overrides.MethodDef.DeclaringType.Module as ModuleDefMD);
					if ((!baseUnderCtrl && ifaceUnderCtrl) || !service.CanRename(slot.MethodDef)) {
						service.SetCanRename(slot.Overrides.MethodDef, false);
					}
					else if (baseUnderCtrl && !ifaceUnderCtrl || !service.CanRename(slot.Overrides.MethodDef)) {
						service.SetCanRename(slot.MethodDef, false);
					}
				}
			}
		}

		public static void Analyze(INameService service, ICollection<ModuleDefMD> modules, MethodDef method) {
			if (!method.IsVirtual)
				return;

			var vTbl = service.GetVTables()[method.DeclaringType];
			var slots = vTbl.FindSlots(method).ToArray();

			bool doesOverridePropertyOrEvent = false;
			var methodProp = method.DeclaringType.Properties.Where(p => BelongsToProperty(p, method));
			foreach (var prop in methodProp) {
				foreach (var baseMethodDef in FindBaseDeclarations(service, method)) {
					var basePropDef = baseMethodDef.DeclaringType.Properties.
						FirstOrDefault(p => BelongsToProperty(p, baseMethodDef) && String.Equals(p.Name, prop.Name, StringComparison.Ordinal));

					if (basePropDef is null) continue;

					// Name of property has to line up.
					service.AddReference(basePropDef, new MemberOverrideReference(prop, basePropDef));
					service.SetCanRename(prop, false);

					// Method names have to line up as well (otherwise inheriting attributes does not work).
					service.AddReference(baseMethodDef, new MemberOverrideReference(method, baseMethodDef));
					service.SetCanRename(method, false);

					doesOverridePropertyOrEvent = true;
				}
			}

			var methodEvent = method.DeclaringType.Events.Where(e => BelongsToEvent(e, method));
			foreach (var evt in methodEvent) {
				foreach (var baseMethodDef in FindBaseDeclarations(service, method)) {
					var baseEventDef = baseMethodDef.DeclaringType.Events.
						FirstOrDefault(e => BelongsToEvent(e, baseMethodDef) && String.Equals(e.Name, evt.Name, StringComparison.Ordinal));

					if (baseEventDef is null) continue;

					// Name of event has to line up.
					service.AddReference(baseEventDef, new MemberOverrideReference(evt, baseEventDef));
					service.SetCanRename(evt, false);

					// Method names have to line up as well (otherwise inheriting attributes does not work).
					service.AddReference(baseMethodDef, new MemberOverrideReference(method, baseMethodDef));
					service.SetCanRename(method, false);

					doesOverridePropertyOrEvent = true;
				}
			}

			if (!method.IsAbstract) {
				foreach (var slot in slots) {
					if (slot.Overrides == null)
						continue;

					SetupOverwriteReferences(service, modules, slot, method.Module);
				}
			}
			else if (!doesOverridePropertyOrEvent) {
				foreach (var baseMethodDef in FindBaseDeclarations(service, method)) {
					service.AddReference(baseMethodDef, new MemberOverrideReference(method, baseMethodDef));
					service.SetCanRename(method, false);
				}
			}
		}

		private static IEnumerable<MethodDef> FindBaseDeclarations(INameService service, MethodDef method) {
			var unprocessed = new Queue<MethodDef>();
			unprocessed.Enqueue(method);

			var vTables = service.GetVTables();

			while (unprocessed.Any()) {
				var currentMethod = unprocessed.Dequeue();

				var vTbl = vTables[currentMethod.DeclaringType];
				var slots = vTbl.FindSlots(currentMethod).Where(s => s.Overrides != null).ToArray();
				if (slots.Any()) {
					foreach (var slot in slots) {
						unprocessed.Enqueue(slot.Overrides.MethodDef);
					}
				}
				else if (method != currentMethod) {
					yield return currentMethod;
				}
			}
		}

		private static bool BelongsToProperty(PropertyDef propertyDef, MethodDef methodDef) =>
			propertyDef.GetMethods.Contains(methodDef) || propertyDef.SetMethods.Contains(methodDef) ||
			(propertyDef.HasOtherMethods && propertyDef.OtherMethods.Contains(methodDef));

		private static bool BelongsToEvent(EventDef eventDef, MethodDef methodDef) =>
			Equals(eventDef.AddMethod, methodDef) || Equals(eventDef.RemoveMethod, methodDef) || Equals(eventDef.InvokeMethod, methodDef) ||
			(eventDef.HasOtherMethods && eventDef.OtherMethods.Contains(methodDef));

		private static void AddImportReference(INameService service, ICollection<ModuleDefMD> modules, ModuleDef module, MethodDef method, MemberRef methodRef) {
			if (method.Module != module && modules.Contains((ModuleDefMD)module)) {
				var declType = (TypeRef)methodRef.DeclaringType.ScopeType;
				service.AddReference(method.DeclaringType, new TypeRefReference(declType, method.DeclaringType));
				service.AddReference(method, new MemberRefReference(methodRef, method));

				var typeRefs = methodRef.MethodSig.Params.SelectMany(param => param.FindTypeRefs()).ToList();
				typeRefs.AddRange(methodRef.MethodSig.RetType.FindTypeRefs());
				typeRefs.AddRange(methodRef.DeclaringType.ToTypeSig().FindTypeRefs());
				foreach (var typeRef in typeRefs) {
					SetupTypeReference(service, modules, module, typeRef);
				}
			}
		}

		private static void SetupTypeReference(INameService service, ICollection<ModuleDefMD> modules, ModuleDef module, ITypeDefOrRef typeDefOrRef) {
			if (!(typeDefOrRef is TypeRef typeRef)) return;

			var def = typeRef.ResolveTypeDefThrow();
			if (def.Module != module && modules.Contains((ModuleDefMD)def.Module))
				service.AddReference(def, new TypeRefReference(typeRef, def));
		}

		private static GenericInstSig SetupSignatureReferences(INameService service, ICollection<ModuleDefMD> modules, ModuleDef module, GenericInstSig typeSig) {
			var genericType = SetupSignatureReferences(service, modules, module, typeSig.GenericType);
			var genericArguments = typeSig.GenericArguments.Select(a => SetupSignatureReferences(service, modules, module, a)).ToList();
			return new GenericInstSig(genericType, genericArguments);
		}

		private static T SetupSignatureReferences<T>(INameService service, ICollection<ModuleDefMD> modules, ModuleDef module, T typeSig) where T : TypeSig {
			var asTypeRef = typeSig.TryGetTypeRef();
			if (asTypeRef != null) {
				SetupTypeReference(service, modules, module, asTypeRef);
			}
			return typeSig;
		}

		private static void SetupOverwriteReferences(INameService service, ICollection<ModuleDefMD> modules, VTableSlot slot, ModuleDef module) {
			var methodDef = slot.MethodDef;
			var baseSlot = slot.Overrides;
			var baseMethodDef = baseSlot.MethodDef;

			var overrideRef = new OverrideDirectiveReference(slot, baseSlot);
			service.AddReference(methodDef, overrideRef);
			service.AddReference(slot.Overrides.MethodDef, overrideRef);

			var importer = new Importer(module, ImporterOptions.TryToUseTypeDefs);

			IMethodDefOrRef target;
			if (baseSlot.MethodDefDeclType is GenericInstSig declType) {
				var signature = SetupSignatureReferences(service, modules, module, declType);
				MemberRef targetRef = new MemberRefUser(module, baseMethodDef.Name, baseMethodDef.MethodSig, signature.ToTypeDefOrRef());
				targetRef = importer.Import(targetRef);
				service.AddReference(baseMethodDef, new MemberRefReference(targetRef, baseMethodDef));

				target = targetRef;
			}
			else {
				target = baseMethodDef;
				if (target.Module != module) {
					target = (IMethodDefOrRef)importer.Import(baseMethodDef);
					if (target is MemberRef memberRef)
						service.AddReference(baseMethodDef, new MemberRefReference(memberRef, baseMethodDef));
				}
			}

			target.MethodSig = importer.Import(target.MethodSig);
			if (target is MemberRef methodRef)
				AddImportReference(service, modules, module, baseMethodDef, methodRef);

			if (methodDef.Overrides.Any(impl => IsMatchingOverride(impl, target)))
				return;

			methodDef.Overrides.Add(new MethodOverride(methodDef, target));
		}

		private static bool IsMatchingOverride(MethodOverride methodOverride, IMethodDefOrRef targetMethod) {
			SigComparer comparer = default;

			var targetDeclTypeDef = targetMethod.DeclaringType.ResolveTypeDef();
			var overrideDeclTypeDef = methodOverride.MethodDeclaration.DeclaringType.ResolveTypeDef();
			if (!comparer.Equals(targetDeclTypeDef, overrideDeclTypeDef))
				return false;

			var targetMethodSig = targetMethod.MethodSig;
			var overrideMethodSig = methodOverride.MethodDeclaration.MethodSig;
			if (methodOverride.MethodDeclaration.DeclaringType is TypeSpec spec && spec.TypeSig is GenericInstSig genericInstSig) {
				overrideMethodSig = GenericArgumentResolver.Resolve(overrideMethodSig, genericInstSig.GenericArguments);
			}

			return comparer.Equals(targetMethodSig, overrideMethodSig);
		}

		public void PreRename(ConfuserContext context, INameService service, ProtectionParameters parameters, IDnlibDef def) {
			//
		}

		public void PostRename(ConfuserContext context, INameService service, ProtectionParameters parameters, IDnlibDef def) {
			var method = def as MethodDef;
			if (method == null || !method.IsVirtual || method.Overrides.Count == 0)
				return;

			var methods = new HashSet<IMethodDefOrRef>(MethodDefOrRefComparer.Instance);
			method.Overrides
				  .RemoveWhere(impl => MethodDefOrRefComparer.Instance.Equals(impl.MethodDeclaration, method));
		}

		class MethodDefOrRefComparer : IEqualityComparer<IMethodDefOrRef> {
			public static readonly MethodDefOrRefComparer Instance = new MethodDefOrRefComparer();
			MethodDefOrRefComparer() { }

			public bool Equals(IMethodDefOrRef x, IMethodDefOrRef y) {
				return new SigComparer().Equals(x, y) && new SigComparer().Equals(x.DeclaringType, y.DeclaringType);
			}

			public int GetHashCode(IMethodDefOrRef obj) {
				return new SigComparer().GetHashCode(obj) * 5 + new SigComparer().GetHashCode(obj.DeclaringType);
			}
		}
	}
}
