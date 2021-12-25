using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Analysis {
	internal sealed class VTableStorage {
		private Dictionary<TypeDef, VTable> storage = new Dictionary<TypeDef, VTable>();
		internal ILogger Logger { get; }

		public VTableStorage(IServiceProvider provider) : 
			this(provider.GetRequiredService<ILoggerFactory>().CreateLogger("analysis")) { }

		public VTableStorage(ILogger logger) {
			Logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		private VTable GetOrConstruct(TypeDef type) {
			if (!storage.TryGetValue(type, out var ret))
				ret = storage[type] = VTable.ConstructVTable(type, this);
			return ret;
		}

		public VTable GetVTable(ITypeDefOrRef type) {
			switch (type) {
				case null:
					throw new ArgumentNullException(nameof(type));
				case TypeDef typeDef:
					return GetOrConstruct(typeDef);
				case TypeRef typeRef:
					return GetOrConstruct(typeRef.ResolveThrow());
				case TypeSpec typeSpec: {
					var sig = typeSpec.TypeSig;
					switch (sig) {
						case TypeDefOrRefSig typeSig:
							return GetOrConstruct(typeSig.TypeDefOrRef.ResolveTypeDefThrow());
						case GenericInstSig: {
							var genInst = (GenericInstSig)sig;
							var openType = genInst.GenericType.TypeDefOrRef.ResolveTypeDefThrow();
							var vTable = GetOrConstruct(openType);

							return ResolveGenericArgument(openType, genInst, vTable);
						}

						default:
							throw new NotSupportedException("Unexpected type: " + type);
					}
				}

				default:
					throw new NotImplementedException("Unexpected implementation for type: " + type.GetType());
			}
		}

		private static VTableSlot ResolveSlot(TypeDef openType, VTableSlot slot, IList<TypeSig> genArgs) {
			var newSig = GenericArgumentResolver.Resolve(slot.Signature.MethodSig, genArgs);
			TypeSig newDecl = slot.MethodDefDeclType;
			if (new SigComparer().Equals(newDecl, openType))
				newDecl = new GenericInstSig((ClassOrValueTypeSig)openType.ToTypeSig(), genArgs.ToArray());
			else
				newDecl = GenericArgumentResolver.Resolve(newDecl, genArgs);
			return new VTableSlot(newDecl, slot.MethodDef, slot.DeclaringType,
				new VTableSignature(newSig, slot.Signature.Name), slot.Overrides);
		}

		private static VTable ResolveGenericArgument(TypeDef openType, GenericInstSig genInst, VTable vTable) {
			Debug.Assert(new SigComparer().Equals(openType, vTable.Type));
			var ret = new VTable(genInst);
			foreach (VTableSlot slot in vTable.Slots) {
				ret.Slots.Add(ResolveSlot(openType, slot, genInst.GenericArguments));
			}

			foreach (var iface in vTable.InterfaceSlots) {
				ret.InterfaceSlots.Add(GenericArgumentResolver.Resolve(iface.Key, genInst.GenericArguments),
					iface.Value.Select(slot => ResolveSlot(openType, slot, genInst.GenericArguments)).ToList());
			}

			return ret;
		}
	}
}
