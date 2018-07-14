using System.Collections.Generic;
using System.Diagnostics;
using Confuser.Core;
using Confuser.Renamer.References;
using Confuser.Renamer.Services;
using dnlib.DotNet;

namespace Confuser.Renamer.Analyzers {
	internal class VTableAnalyzer : IRenamer {
		public void Analyze(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def) {
			VTable vTbl;

			var nameService = (NameService)service;

			if (def is TypeDef type) {
				if (type.IsInterface)
					return;

				vTbl = nameService.GetVTables()[type];
				foreach (var ifaceVTbl in vTbl.InterfaceSlots.Values) {
					foreach (var slot in ifaceVTbl) {
						if (slot.Overrides == null)
							continue;
						Debug.Assert(slot.Overrides.MethodDef.DeclaringType.IsInterface);
						// A method in base type can implements an interface method for a
						// derived type. If the base type/interface is not in our control, we should
						// not rename the methods.
						bool baseUnderCtrl = context.Modules.Contains(slot.MethodDef.DeclaringType.Module as ModuleDefMD);
						bool ifaceUnderCtrl = context.Modules.Contains(slot.Overrides.MethodDef.DeclaringType.Module as ModuleDefMD);
						if ((!baseUnderCtrl && ifaceUnderCtrl) || !service.CanRename(context, slot.MethodDef)) {
							service.SetCanRename(context, slot.Overrides.MethodDef, false);
						}
						else if (baseUnderCtrl && !ifaceUnderCtrl || !service.CanRename(context, slot.Overrides.MethodDef)) {
							service.SetCanRename(context, slot.MethodDef, false);
						}
					}
				}
			}
			else if (def is MethodDef method) {
				if (!method.IsVirtual)
					return;

				vTbl = nameService.GetVTables()[method.DeclaringType];
				var sig = VTableSignature.FromMethod(method);
				var slots = vTbl.FindSlots(method);

				if (!method.IsAbstract) {
					foreach (var slot in slots) {
						if (slot.Overrides == null)
							continue;
						// Better on safe side, add references to both methods.
						service.AddReference(context, method, new OverrideDirectiveReference(slot, slot.Overrides));
						service.AddReference(context, slot.Overrides.MethodDef, new OverrideDirectiveReference(slot, slot.Overrides));
					}
				}
				else {
					foreach (var slot in slots) {
						if (slot.Overrides == null)
							continue;
						service.SetCanRename(context, method, false);
						service.SetCanRename(context, slot.Overrides.MethodDef, false);
					}
				}
			}
		}

		public void PreRename(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def) {
			//
		}

		public void PostRename(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def) {
			if (!(def is MethodDef method) || !method.IsVirtual || method.Overrides.Count == 0)
				return;

			var methods = new HashSet<IMethodDefOrRef>(MethodDefOrRefComparer.Instance);
			method.Overrides
			      .RemoveWhere(impl => MethodDefOrRefComparer.Instance.Equals(impl.MethodDeclaration, method));
		}

		private sealed class MethodDefOrRefComparer : IEqualityComparer<IMethodDefOrRef> {
			public static readonly MethodDefOrRefComparer Instance = new MethodDefOrRefComparer();
			MethodDefOrRefComparer() { }

			public bool Equals(IMethodDefOrRef x, IMethodDefOrRef y) =>
				new SigComparer().Equals(x, y) && new SigComparer().Equals(x.DeclaringType, y.DeclaringType);

			public int GetHashCode(IMethodDefOrRef obj) =>
				new SigComparer().GetHashCode(obj) * 5 + new SigComparer().GetHashCode(obj.DeclaringType);
		}
	}
}
