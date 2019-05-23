using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Confuser.Core;
using Confuser.Renamer;
using Confuser.Renamer.Services;
using dnlib.DotNet;

namespace Confuser.Protections.ReferenceProxy {
	internal abstract partial class RPMode {
		public abstract void ProcessCall(RPContext ctx, int instrIndex);

		static ITypeDefOrRef Import(RPContext ctx, TypeDef typeDef) {
			ITypeDefOrRef retTypeRef = new Importer(ctx.Module, ImporterOptions.TryToUseTypeDefs).Import(typeDef);
			if (typeDef.Module != ctx.Module && ctx.Context.Modules.Contains((ModuleDefMD)typeDef.Module))
				ctx.Name?.AddReference(ctx.Context, typeDef, new TypeRefReference((TypeRef)retTypeRef, typeDef));
			return retTypeRef;
		}

		protected static MethodSig CreateProxySignature(RPContext ctx, IMethod method, bool newObj) {
			ModuleDef module = ctx.Module;
			if (newObj) {
				Debug.Assert(method.MethodSig.HasThis);
				Debug.Assert(method.Name == ".ctor");
				TypeSig[] paramTypes = method.MethodSig.Params.Select(type => {
					if (ctx.TypeErasure && type.IsClassSig && method.MethodSig.HasThis)
						return module.CorLibTypes.Object;
					return type;
				}).ToArray();

				TypeSig retType;
				if (ctx.TypeErasure) // newobj will not be used with value types
					retType = module.CorLibTypes.Object;
				else {
					TypeDef declType = method.DeclaringType.ResolveTypeDefThrow();
					retType = Import(ctx, declType).ToTypeSig();
				}

				return MethodSig.CreateStatic(retType, paramTypes);
			}
			else {
				IEnumerable<TypeSig> paramTypes = method.MethodSig.Params.Select(type => {
					if (ctx.TypeErasure && type.IsClassSig && method.MethodSig.HasThis)
						return module.CorLibTypes.Object;
					return type;
				});
				if (method.MethodSig.HasThis && !method.MethodSig.ExplicitThis) {
					TypeDef declType = method.DeclaringType.ResolveTypeDefThrow();
					if (ctx.TypeErasure && !declType.IsValueType)
						paramTypes = new[] {module.CorLibTypes.Object}.Concat(paramTypes);
					else
						paramTypes = new[] {Import(ctx, declType).ToTypeSig()}.Concat(paramTypes);
				}

				TypeSig retType = method.MethodSig.RetType;
				if (ctx.TypeErasure && retType.IsClassSig)
					retType = module.CorLibTypes.Object;
				return MethodSig.CreateStatic(retType, paramTypes.ToArray());
			}
		}
	}
}
