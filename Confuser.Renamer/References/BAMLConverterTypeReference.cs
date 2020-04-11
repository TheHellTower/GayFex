using System;
using Confuser.Core;
using Confuser.Renamer.BAML;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	internal class BAMLConverterTypeReference : INameReference<TypeDef> {
		readonly PropertyRecord propRec;
		readonly TypeSig sig;
		readonly TextRecord textRec;
		readonly BAMLAnalyzer.XmlNsContext xmlnsCtx;

		public BAMLConverterTypeReference(BAMLAnalyzer.XmlNsContext xmlnsCtx, TypeSig sig, PropertyRecord rec) {
			this.xmlnsCtx = xmlnsCtx;
			this.sig = sig;
			propRec = rec;
		}

		public BAMLConverterTypeReference(BAMLAnalyzer.XmlNsContext xmlnsCtx, TypeSig sig, TextRecord rec) {
			this.xmlnsCtx = xmlnsCtx;
			this.sig = sig;
			textRec = rec;
		}

		public bool UpdateNameReference(ConfuserContext context, INameService service) {
			string name = sig.ReflectionName;
			var assembly = sig.ToBasicTypeDefOrRef().ResolveTypeDefThrow().Module.Assembly;
			string prefix = xmlnsCtx.GetPrefix(sig.ReflectionNamespace, sig.ToBasicTypeDefOrRef().ResolveTypeDefThrow().Module.Assembly);
			if (!string.IsNullOrEmpty(prefix)) {
				name = prefix + ":" + name;
				xmlnsCtx.AddNsMap(sig.ReflectionNamespace, assembly, prefix);
			}
			if (propRec != null) {
				if (string.Equals(propRec.Value, name, StringComparison.Ordinal)) return false;
				propRec.Value = name;
			}
			else {
				if (string.Equals(textRec.Value, name, StringComparison.Ordinal)) return false;
				textRec.Value = name;
			}

			return true;
		}

		public bool ShouldCancelRename() => false;
	}
}
