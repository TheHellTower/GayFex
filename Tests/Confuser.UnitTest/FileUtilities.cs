using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Confuser.UnitTest {
	public static class FileUtilities {
		public static void ClearOutput(string outputFile) {
			for (var i = 0; i < 10; i++) {
				if (ClearOutputInternal(outputFile)) return;
				Task.Delay(500);
			}
		}

		private static bool ClearOutputInternal(string outputFile) {
			try {
				if (File.Exists(outputFile)) {
					File.Delete(outputFile);
				}
			}
			catch (UnauthorizedAccessException) { }
			var debugSymbols = Path.ChangeExtension(outputFile, "pdb");
			try {
				if (File.Exists(debugSymbols)) {
					File.Delete(debugSymbols);
				}
			}
			catch (UnauthorizedAccessException) { }

			try {
				Directory.Delete(Path.GetDirectoryName(outputFile), true);
			}
			catch (IOException) { return false; }
			catch (UnauthorizedAccessException) { return false; }
			return true;
		}

		public static byte[] ComputeFileChecksum(string file) {
			if (file == null) throw new ArgumentNullException(nameof(file));
			if (!File.Exists(file)) throw new FileNotFoundException($"File: {file}");

			using (var checksum = SHA256.Create()) {
				using (var fs = File.OpenRead(file)) {
					return checksum.ComputeHash(fs);
				}
			}
		}
	}
}
