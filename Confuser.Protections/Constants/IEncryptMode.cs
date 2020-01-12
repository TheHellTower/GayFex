using Confuser.Helpers;

namespace Confuser.Protections.Constants {
	internal interface IEncryptMode {
		CryptProcessor EmitDecrypt(CEContext ctx);
		uint[] Encrypt(uint[] data, int offset, uint[] key);
	}
}
