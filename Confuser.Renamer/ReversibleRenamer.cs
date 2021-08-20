using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Confuser.Renamer {
	public class ReversibleRenamer {
		readonly Aes cipher;
		readonly byte[] key;

		public ReversibleRenamer(string password) {
			cipher = Aes.Create();
			using (var sha = SHA256.Create())
				cipher.Key = key = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
		}

		public string Encrypt(string name) {
			byte ivId = GetIVId(name);
			cipher.IV = GetIV(ivId);
			var buf = Encoding.UTF8.GetBytes(name);

			using (var ms = new MemoryStream()) {
				ms.WriteByte(ivId);
				using (var stream = new CryptoStream(ms, cipher.CreateEncryptor(), CryptoStreamMode.Write)) {
					stream.Write(buf, 0, buf.Length);
					stream.FlushFinalBlock();
					return Base64Encode(ms.GetBuffer(), (int)ms.Length);
				}
			}
		}

		public string Decrypt(string name) {
			using (var ms = new MemoryStream(Base64Decode(name))) {
				byte ivId = (byte)ms.ReadByte();
				cipher.IV = GetIV(ivId);

				using (var result = new MemoryStream()) {
					using (var stream = new CryptoStream(ms, cipher.CreateDecryptor(), CryptoStreamMode.Read))
						stream.CopyTo(result);
					return Encoding.UTF8.GetString(result.GetBuffer(), 0, (int)result.Length);
				}
			}
		}

		byte[] GetIV(byte ivId) {
			byte[] iv = new byte[cipher.BlockSize / 8];
			for (int i = 0; i < iv.Length; i++)
				iv[i] = (byte)(ivId ^ key[i]);
			return iv;
		}

		byte GetIVId(string str) {
			byte x = (byte)str[0];
			for (int i = 1; i < str.Length; i++)
				x = (byte)(x * 3 + (byte)str[i]);
			return x;
		}

		static string Base64Encode(byte[] buffer, int length) {
			int inputUnpaddedLength = 4 * length / 3;
			var outArray = new char[(inputUnpaddedLength + 3) & ~3];
			Convert.ToBase64CharArray(buffer, 0, length, outArray, 0);

			var result = new StringBuilder(inputUnpaddedLength);
			foreach (var oldChar in outArray) {
				if (oldChar == '=') {
					break;
				}

				result.Append(oldChar == '+'
					? '$'
					: oldChar == '/'
						? '_'
						: oldChar);
			}

			return result.ToString();
		}

		static byte[] Base64Decode(string str) {
			var newLength = (str.Length + 3) & ~3;
			var inArray = new char[newLength];
			for (int index = 0; index < newLength; index++) {
				char newChar;
				if (index < str.Length) {
					char oldChar = str[index];
					newChar = oldChar == '$'
						? '+'
						: oldChar == '_'
							? '/'
							: oldChar;
				}
				else {
					newChar = '=';
				}

				inArray[index] = newChar;
			}

			return Convert.FromBase64CharArray(inArray, 0, inArray.Length);
		}
	}
}
