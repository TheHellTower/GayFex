using Confuser.Core;

namespace Confuser.Renamer {
	internal sealed class NameProtectionParameters : ProtectionParametersBase {
		internal IProtectionParameter<bool> RickRoll { get; } = ProtectionParameter.Boolean("rickRoll", false);
		internal IProtectionParameter<bool> RenameArguments { get; } = ProtectionParameter.Boolean("renameArgs", true);
		internal IProtectionParameter<bool> RenamePdb { get; } = ProtectionParameter.Boolean("renPdb", false);
		internal IProtectionParameter<bool> FlattenNamespace { get; } = ProtectionParameter.Boolean("flatten", true);
		internal IProtectionParameter<RenameMode> Mode { get; } = ProtectionParameter.Enum("mode", RenameMode.Unicode);
		internal IProtectionParameter<string> Password { get; } = ProtectionParameter.String("password", "");
		internal IProtectionParameter<bool> ForceRename { get; } = ProtectionParameter.Boolean("forceRen", false);
		internal IProtectionParameter<uint> IdOffset { get; } = ProtectionParameter.UInteger("idOffset", 0);

		internal IProtectionParameter<string> SymbolMapFileName { get; } =
			ProtectionParameter.String("mapFileName", "symbols.map");

		internal IProtectionParameter<bool> RenameXaml { get; } = ProtectionParameter.Boolean("renXaml", true);
	}
}
