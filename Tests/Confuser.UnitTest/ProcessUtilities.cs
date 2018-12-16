using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Confuser.UnitTest {
	public delegate Task OutputHandler(StreamReader stdOut);
	public delegate Task<TResult> OutputHandler<TResult>(StreamReader stdout);

	public static class ProcessUtilities {
		public static async Task<int> ExecuteTestApplication(string file, OutputHandler outputHandler, ITestOutputHelper outputHelper) {
			var result = await ExecuteTestApplication(file, async (stdout) => {
				await outputHandler(stdout).ConfigureAwait(false);
				return true;
			}, outputHelper).ConfigureAwait(false);
			return result.ExitCode;
		}

		public static async Task<(int ExitCode, TResult Result)> ExecuteTestApplication<TResult>(string file, OutputHandler<TResult> outputHandler, ITestOutputHelper outputHelper) {
			var info = new ProcessStartInfo() {
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8,
				UseShellExecute = false,
				WindowStyle = ProcessWindowStyle.Hidden,
				CreateNoWindow = true
			};
			if (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
				info.FileName = "dotnet";
				info.Arguments = '"' + file + '"';
			}
			else {
				info.FileName = "cmd";
				info.Arguments = "/c \"" + file + '"';
			}

			outputHelper.WriteLine("Executing test application: {0} {1}", info.FileName, info.Arguments);

			using (var process = Process.Start(info)) {
				var stdout = process.StandardOutput;
				var stderr = process.StandardError;
				TResult result;
				try {
					result = await outputHandler(stdout).ConfigureAwait(false);
					Assert.Empty(await stdout.ReadToEndAsync().ConfigureAwait(false));
					Assert.Empty(await stderr.ReadToEndAsync().ConfigureAwait(false));
				}
				catch {
					var cnt = 0;
					while (!process.HasExited && ++cnt < 10) {
						await Task.Delay(500).ConfigureAwait(false);
					}
					outputHelper.WriteLine("Remaining output: {0}", await stdout.ReadToEndAsync().ConfigureAwait(false));
					outputHelper.WriteLine("Remaining error: {0}", await stderr.ReadToEndAsync().ConfigureAwait(false));
					if (process.HasExited)
						outputHelper.WriteLine("Process exit code: {0:d}", process.ExitCode);
					else
						outputHelper.WriteLine("Process has not exited.");
					throw;
				}

				Assert.True(process.HasExited);
				return (process.ExitCode, result);
			}
		}
	}
}
