using System.Diagnostics;
using System.Text;
using Confuser.Core;
using Confuser.Core.Services;

namespace Confuser.Protections.Constants {
	internal sealed class ConstantProtectionParameters : ProtectionParametersBase {
		internal IProtectionParameter<bool> ControlFlowGraphReplacement { get; } =
			ProtectionParameter.Boolean("cfg", false);

		internal IProtectionParameter<Mode> Mode { get; } = ProtectionParameter.Enum("mode", Constants.Mode.Normal);
		internal IProtectionParameter<uint> DecoderCount { get; } = ProtectionParameter.UInteger("decoderCount", 5);
		internal IProtectionParameter<EncodeElements> Elements { get; } = new EncodeElementsProtectionParameter();
		internal IProtectionParameter<CompressionAlgorithm> Compressor { get; } = ProtectionParameter.Enum("compressor", CompressionAlgorithm.Lzma);
		internal IProtectionParameter<CompressionMode> Compress { get; } = ProtectionParameter.Enum("compress", CompressionMode.Auto);

		private sealed class EncodeElementsProtectionParameter : IProtectionParameter<EncodeElements> {
			EncodeElements IProtectionParameter<EncodeElements>.DefaultValue =>
				EncodeElements.Strings | EncodeElements.Initializers;

			string IProtectionParameter.Name => "elements";

			EncodeElements IProtectionParameter<EncodeElements>.Deserialize(string serializedValue) {
				var result = EncodeElements.None;
				foreach (char elem in serializedValue?.ToUpperInvariant() ?? string.Empty)
					switch (elem) {
						case 'S':
							result |= EncodeElements.Strings;
							break;
						case 'N':
							result |= EncodeElements.Numbers;
							break;
						case 'P':
							result |= EncodeElements.Primitive;
							break;
						case 'I':
							result |= EncodeElements.Initializers;
							break;
						default:
							Debug.Fail("Unexpected encode value: " + elem);
							break;
					}

				return result;
			}

			string IProtectionParameter<EncodeElements>.Serialize(EncodeElements value) {
				var resultBuilder = new StringBuilder(4);
				if ((value & EncodeElements.Strings) == EncodeElements.Strings)
					resultBuilder.Append('S');
				if ((value & EncodeElements.Numbers) == EncodeElements.Numbers)
					resultBuilder.Append('N');
				if ((value & EncodeElements.Primitive) == EncodeElements.Primitive)
					resultBuilder.Append('P');
				if ((value & EncodeElements.Initializers) == EncodeElements.Initializers)
					resultBuilder.Append('I');
				return resultBuilder.ToString();
			}
		}
	}
}
