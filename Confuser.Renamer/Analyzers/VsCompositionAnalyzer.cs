using System;
using System.Linq;
using Confuser.Core;
using Confuser.Renamer.References;
using Confuser.Renamer.Services;
using dnlib.DotNet;

namespace Confuser.Renamer.Analyzers {
	internal sealed class VsCompositionAnalyzer : IRenamer {
		/// <inheritdoc />
		void IRenamer.Analyze(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def) {
			if (!(def is TypeDef typeDef)) return;

			Analyze(context, service, typeDef);
		}

		public static void Analyze(IConfuserContext context, INameService service, TypeDef typeDef) {
			if (typeDef.InheritsFrom("System.ComponentModel.Composition.ExportAttribute")) {
				// This type is an export attribute. In case it implements interfaces, these interfaces may be used as Metadata.
				// If that is the case, the implementation of the meta data handling requires that the getter methods
				// of the properties are starting their name with "get_".
				// Reference:
				// https://github.com/microsoft/vs-mef/blob/dc35edfa2c49ae2e20dc8fde2dec59c373062f32/src/Microsoft.VisualStudio.Composition/Configuration/ExportMetadataViewInterfaceEmitProxy.cs#L49-L50
				foreach (var ifcProps in typeDef.Interfaces.SelectMany(i => i.Interface.ResolveTypeDefThrow().Properties)) {
					var getter = ifcProps.GetMethod;
					if (getter != null) {
						service.AddReference(context, getter, new RequiredPrefixReference<MethodDef>(getter, "get_"));
					}
				}
			}
		}

		/// <inheritdoc />
		public void PreRename(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def) { }

		/// <inheritdoc />
		public void PostRename(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def) { }
	}
}
