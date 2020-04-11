using System;
using System.Globalization;
using Confuser.Core;
using Confuser.Renamer.BAML;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	internal class BAMLPathTypeReference : INameReference<TypeDef> {
		private SourceValueInfo? propertyInfo;
		private IndexerParamInfo? indexerInfo;
		private readonly TypeSig sig;
		private readonly BAMLAnalyzer.XmlNsContext xmlnsCtx;

		private BAMLPathTypeReference(BAMLAnalyzer.XmlNsContext xmlnsCtx, TypeSig sig) {
			if (xmlnsCtx == null) throw new ArgumentNullException(nameof(xmlnsCtx));
			if (sig == null) throw new ArgumentNullException(nameof(sig));

			this.xmlnsCtx = xmlnsCtx;
			this.sig = sig;
		}

		public BAMLPathTypeReference(BAMLAnalyzer.XmlNsContext xmlnsCtx, TypeSig sig, IndexerParamInfo indexerInfo) : this(xmlnsCtx, sig) => 
			this.indexerInfo = indexerInfo;

		public BAMLPathTypeReference(BAMLAnalyzer.XmlNsContext xmlnsCtx, TypeSig sig, SourceValueInfo propertyInfo) : this(xmlnsCtx, sig) => 
			this.propertyInfo = propertyInfo;

		public bool UpdateNameReference(ConfuserContext context, INameService service) {
			string name = sig.ReflectionName;
			string prefix = xmlnsCtx.GetPrefix(sig.ReflectionNamespace, sig.ToBasicTypeDefOrRef().ResolveTypeDefThrow().Module.Assembly);
			if (!string.IsNullOrEmpty(prefix))
				name = prefix + ":" + name;

			if (indexerInfo != null) {
				var info = indexerInfo.Value;
				if (string.Equals(info.parenString, name, StringComparison.Ordinal)) return false;
				info.parenString = name;
				indexerInfo = info;
			}
			else {
				var info = propertyInfo.Value;
				var propertyName = info.GetPropertyName();
				var newName = string.Format(CultureInfo.InvariantCulture, "({0}.{1})", name, propertyName);
				if (string.Equals(info.name, newName, StringComparison.Ordinal)) return false;
				info.name = newName;
				propertyInfo = info;
			}
			return true;
		}

		public bool ShouldCancelRename() => false;
	}
}
