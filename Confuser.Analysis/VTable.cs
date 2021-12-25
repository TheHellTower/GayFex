using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using dnlib.DotNet;
using Microsoft.Extensions.Logging;

namespace Confuser.Analysis {

	internal partial class VTable : IVTable {
		internal VTable(TypeSig type) {
			Type = type;
			Slots = new List<VTableSlot>();
			InterfaceSlots = new Dictionary<TypeSig, List<VTableSlot>>(TypeEqualityComparer.Instance);
		}

		public TypeSig Type { get; private set; }

		public List<VTableSlot> Slots { get; private set; }
		public Dictionary<TypeSig, List<VTableSlot>> InterfaceSlots { get; private set; }

		IReadOnlyList<IVTableSlot> IVTable.Slots => Slots;

		IReadOnlyDictionary<TypeSig, IReadOnlyList<IVTableSlot>> IVTable.InterfaceSlots => new ReadOnlyInterfaceSlotsDictionary(InterfaceSlots);

		public IEnumerable<VTableSlot> FindSlots(IMethod method) {
			return Slots
				.Concat(InterfaceSlots.SelectMany(iface => iface.Value))
				.Where(slot => MethodEqualityComparer.CompareDeclaringTypes.Equals(slot.MethodDef, method));
		}

		public static VTable ConstructVTable(TypeDef typeDef, VTableStorage storage) {
			var ret = new VTable(typeDef.ToTypeSig());

			var virtualMethods = typeDef.Methods
				.Where(method => method.IsVirtual)
				.ToDictionary(
					method => VTableSignature.FromMethod(method),
					method => method
				);

			// See Partition II 12.2 for implementation algorithm
			VTableConstruction vTbl = new VTableConstruction();

			// Inherits base type's slots
			VTable baseVTbl = storage.GetVTable(typeDef.GetBaseTypeThrow());
			if (baseVTbl != null) {
				Inherits(vTbl, baseVTbl);
			}

			// Explicit interface implementation
			foreach (InterfaceImpl iface in typeDef.Interfaces) {
				VTable ifaceVTbl = storage.GetVTable(iface.Interface);
				if (ifaceVTbl != null) {
					Implements(vTbl, virtualMethods, ifaceVTbl, iface.Interface.ToTypeSig());
				}
			}

			// Normal interface implementation
			if (!typeDef.IsInterface) {
				// Interface methods cannot implements base interface methods.

				foreach (var interfaceTypeSig in vTbl.InterfaceSlots.Keys.ToList()) {
					var slots = vTbl.InterfaceSlots[interfaceTypeSig];
					if (slots.Select(g => g.Key)
						.Any(sig => virtualMethods.ContainsKey(sig) || vTbl.SlotsMap.ContainsKey(sig))) {
						// Something has a new signature. We need to rewrite the whole thing.
						
						// This is the step 1 of 12.2 algorithm -- find implementation for still empty slots.
						// Note that it seems we should include newslot methods as well, despite what the standard said.
						slots = slots
							.SelectMany(g => g.Select(slot => (g.Key, Slot: slot)))
							.ToLookup(t => t.Key, t => {
								if (!t.Slot.MethodDef.DeclaringType.IsInterface)
									return t.Slot;

								if (virtualMethods.TryGetValue(t.Key, out var impl))
									return t.Slot.OverridedBy(impl);

								if (vTbl.SlotsMap.TryGetValue(t.Key, out var implSlot))
									return t.Slot.OverridedBy(implSlot.MethodDef);

								return t.Slot;
							});

						vTbl.InterfaceSlots[interfaceTypeSig] = slots;
					}
				}
			}

			// Normal overrides
			foreach (var method in virtualMethods) {
				VTableSlot slot;
				if (method.Value.IsNewSlot) {
					slot = new VTableSlot(method.Value, typeDef.ToTypeSig(), method.Key);
				}
				else {
					if (vTbl.SlotsMap.TryGetValue(method.Key, out slot)) {
						Debug.Assert(!slot.MethodDef.IsFinal);
						slot = slot.OverridedBy(method.Value);
					}
					else
						slot = new VTableSlot(method.Value, typeDef.ToTypeSig(), method.Key);
				}

				vTbl.SlotsMap[method.Key] = slot;
				vTbl.AllSlots.Add(slot);
			}

			// MethodImpls
			foreach (var method in virtualMethods) {
				foreach (var impl in method.Value.Overrides) {
					Debug.Assert(impl.MethodBody == method.Value);

					MethodDef targetMethod = impl.MethodDeclaration.ResolveThrow();
					if (targetMethod.DeclaringType.IsInterface) {
						var iface = impl.MethodDeclaration.DeclaringType.ToTypeSig();
						CheckKeyExist(storage, vTbl.InterfaceSlots, iface, "MethodImpl Iface");
						var ifaceVTbl = vTbl.InterfaceSlots[iface];

						var signature = VTableSignature.FromMethod(impl.MethodDeclaration);
						CheckKeyExist(storage, ifaceVTbl, signature, "MethodImpl Iface Sig");

						vTbl.InterfaceSlots[iface] = ifaceVTbl
							.SelectMany(g => g.Select(slot => (g.Key, Slot: slot)))
							.ToLookup(t => t.Key, t => {
								if (!t.Key.Equals(signature)) 
									return t.Slot;

								var targetSlot = t.Slot;
								while (targetSlot.Overrides != null)
									targetSlot = targetSlot.Overrides;
								Debug.Assert(targetSlot.MethodDef.DeclaringType.IsInterface);
								Debug.Assert(targetSlot.Signature.Equals(t.Slot.Signature));

								return targetSlot.OverridedBy(method.Value);
							});
					}
					else {
						var targetSlot = vTbl.AllSlots.SingleOrDefault(slot => slot.MethodDef == targetMethod);
						if (targetSlot == null) {
							throw new Exception($"method [{method}] not found.");
						}
						CheckKeyExist(storage, vTbl.SlotsMap, targetSlot.Signature, "MethodImpl Normal Sig");
						targetSlot = vTbl.SlotsMap[targetSlot.Signature]; // Use the most derived slot
						// Maybe implemented by above processes --- this process should take priority
						while (targetSlot.MethodDef.DeclaringType == typeDef)
							targetSlot = targetSlot.Overrides;
						vTbl.SlotsMap[targetSlot.Signature] = targetSlot.OverridedBy(method.Value);
					}
				}
			}

			// Populate result V-table
			ret.InterfaceSlots = vTbl.InterfaceSlots.ToDictionary(
				kvp => kvp.Key, kvp => kvp.Value.SelectMany(g => g).ToList(), TypeEqualityComparer.Instance);

			foreach (var slot in vTbl.AllSlots) {
				ret.Slots.Add(slot);
			}

			return ret;
		}

		private static void Implements(VTableConstruction vTbl, Dictionary<VTableSignature, MethodDef> virtualMethods,
			VTable ifaceVTbl, TypeSig iface) {
			// This is the step 2 of 12.2 algorithm -- use virtual newslot methods for explicit implementation.

			Func<VTableSlot, VTableSlot> implLookup = slot => {
				MethodDef impl;
				if (virtualMethods.TryGetValue(slot.Signature, out impl) &&
					impl.IsNewSlot && !impl.DeclaringType.IsInterface) {
					// Interface methods cannot implements base interface methods.
					// The Overrides of interface slots should directly points to the root interface slot
					var targetSlot = slot;
					while (targetSlot.Overrides != null && !targetSlot.MethodDef.DeclaringType.IsInterface)
						targetSlot = targetSlot.Overrides;
					Debug.Assert(targetSlot.MethodDef.DeclaringType.IsInterface);
					return targetSlot.OverridedBy(impl);
				}

				return slot;
			};

			if (vTbl.InterfaceSlots.ContainsKey(iface)) {
				vTbl.InterfaceSlots[iface] = vTbl.InterfaceSlots[iface].SelectMany(g => g).ToLookup(
					slot => slot.Signature, implLookup);
			}
			else {
				vTbl.InterfaceSlots.Add(iface, ifaceVTbl.Slots.ToLookup(
					slot => slot.Signature, implLookup));
			}

			foreach (var baseIface in ifaceVTbl.InterfaceSlots) {
				if (vTbl.InterfaceSlots.ContainsKey(baseIface.Key)) {
					vTbl.InterfaceSlots[baseIface.Key] = vTbl.InterfaceSlots[baseIface.Key].SelectMany(g => g).ToLookup(
						slot => slot.Signature, implLookup);
				}
				else {
					vTbl.InterfaceSlots.Add(baseIface.Key, baseIface.Value.ToLookup(
						slot => slot.Signature, implLookup));
				}
			}
		}

		private static void Inherits(VTableConstruction vTbl, VTable baseVTbl) {
			foreach (VTableSlot slot in baseVTbl.Slots) {
				vTbl.AllSlots.Add(slot);
				// It's possible to have same signature in multiple slots,
				// when a derived type shadow the base type using newslot.
				// In this case, use the derived type's slot in SlotsMap.

				// The derived type's slots are always at a later position
				// than the base type, so it would naturally 'override'
				// their position in SlotsMap.
				vTbl.SlotsMap[slot.Signature] = slot;
			}

			// This is the step 1 of 12.2 algorithm -- copy the base interface implementation.
			foreach (var iface in baseVTbl.InterfaceSlots) {
				Debug.Assert(!vTbl.InterfaceSlots.ContainsKey(iface.Key));
				vTbl.InterfaceSlots.Add(iface.Key, iface.Value.ToLookup(slot => slot.Signature, slot => slot));
			}
		}

		[Conditional("DEBUG")]
		private static void CheckKeyExist<TKey, TValue>(VTableStorage storage, IDictionary<TKey, TValue> dictionary, TKey key,
			string name) {
			if (!dictionary.ContainsKey(key)) {
				storage.Logger.LogError("{0} not found: {1}", name, key);
				foreach (var k in dictionary.Keys)
					storage.Logger.LogError("    {0}", k);
			}
		}

		[Conditional("DEBUG")]
		private static void CheckKeyExist<TKey, TValue>(VTableStorage storage, ILookup<TKey, TValue> lookup, TKey key, string name) {
			if (!lookup.Contains(key)) {
				storage.Logger.LogError("{0} not found: {1}", name, key);
				foreach (var k in lookup.Select(g => g.Key))
					storage.Logger.LogError("    {0}", k);
			}
		}
	}
}
