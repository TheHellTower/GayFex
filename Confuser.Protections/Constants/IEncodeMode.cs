using Confuser.Helpers;

namespace Confuser.Protections.Constants {
	internal interface IEncodeMode {
		(PlaceholderProcessor Processor, object Data) CreateDecoder(CEContext ctx);
		uint Encode(object data, CEContext ctx, uint id);
	}
}
