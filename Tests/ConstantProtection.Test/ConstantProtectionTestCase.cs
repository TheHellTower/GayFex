using System.Collections.Generic;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.Core.Services;
using Confuser.Protections.Constants;
using Xunit.Abstractions;

namespace ConstantProtection.Test {
	public sealed record ConstantProtectionTestCase : IXunitSerializable {
		internal static ConstantProtectionParameters Parameters { get; } = new ConstantProtectionParameters();

		internal string Framework { get; set; }
		internal Mode Mode { get; set; }
		internal CompressionAlgorithm Compressor { get; set; }
		internal bool ControlFlowGraph { get; set; }
		internal EncodeElements Elements { get; set; }

		public void Deserialize(IXunitSerializationInfo info) {
			Framework = info.GetValue<string>(nameof(Framework));
			Mode = Parameters.Mode.Deserialize(info.GetValue<string>(nameof(Mode)));
			Compressor = Parameters.Compressor.Deserialize(info.GetValue<string>(nameof(Compressor)));
			ControlFlowGraph = Parameters.ControlFlowGraphReplacement.Deserialize(info.GetValue<string>(nameof(ControlFlowGraph)));
			Elements = Parameters.Elements.Deserialize(info.GetValue<string>(nameof(Elements)));
		}

		public void Serialize(IXunitSerializationInfo info) {
			info.AddValue(nameof(Framework), Framework);
			info.AddValue(nameof(Mode), Parameters.Mode.Serialize(Mode));
			info.AddValue(nameof(Compressor), Parameters.Compressor.Serialize(Compressor));
			info.AddValue(nameof(ControlFlowGraph), Parameters.ControlFlowGraphReplacement.Serialize(ControlFlowGraph));
			info.AddValue(nameof(Elements), Parameters.Elements.Serialize(Elements));
		}

		internal SettingItem<IProtection> Apply(SettingItem<IProtection> settings) {
			settings.Add(Parameters.Mode.Name, Parameters.Mode.Serialize(Mode));
			settings.Add(Parameters.Compressor.Name, Parameters.Compressor.Serialize(Compressor));
			settings.Add(Parameters.ControlFlowGraphReplacement.Name, Parameters.ControlFlowGraphReplacement.Serialize(ControlFlowGraph));
			settings.Add(Parameters.Elements.Name, Parameters.Elements.Serialize(Elements));
			return settings;
		}

		public override string ToString() =>
			$"{Framework}, mode: {Mode}, compression: {Compressor}, cfg: {ControlFlowGraph}, elements: {Elements}";
	}
}
