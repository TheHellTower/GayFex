using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Confuser.UnitTest {
	public abstract class TestBase {
		private const string _externalPrefix = "external:";

		protected readonly ITestOutputHelper outputHelper;

		protected static IEnumerable<SettingItem<IProtection>> NoProtections => Enumerable.Empty<SettingItem<IProtection>>();

		protected TestBase(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		protected Task Run(string inputFileName, string[] expectedOutput, SettingItem<IProtection> protection,
			string outputDirSuffix = "", Action<string> outputAction = null, SettingItem<IPacker> packer = null,
			Action<ProjectModule> projectModuleAction = null, Func<string, Task> postProcessAction = null,
			string seed = null, bool checkOutput = true) =>

			Run(null, inputFileName, expectedOutput, protection, outputDirSuffix, outputAction, packer, projectModuleAction, postProcessAction, seed, checkOutput);

		protected Task Run(string framework, string inputFileName, string[] expectedOutput, SettingItem<IProtection> protection,
			string outputDirSuffix = "", Action<string> outputAction = null, SettingItem<IPacker> packer = null,
			Action<ProjectModule> projectModuleAction = null, Func<string, Task> postProcessAction = null,
			string seed = null, bool checkOutput = true) =>

			Run(framework, new[] { inputFileName }, expectedOutput, protection, outputDirSuffix, outputAction, packer,
				projectModuleAction, postProcessAction, seed, checkOutput);

		protected Task Run(string inputFileName, string[] expectedOutput, IEnumerable<SettingItem<IProtection>> protections,
			string outputDirSuffix = "", Action<string> outputAction = null, SettingItem<IPacker> packer = null,
			Action<ProjectModule> projectModuleAction = null, Func<string, Task> postProcessAction = null,
			string seed = null, bool checkOutput = true) =>

			Run(null, inputFileName, expectedOutput, protections, outputDirSuffix, outputAction, packer,
				projectModuleAction, postProcessAction, seed, checkOutput);

		protected Task Run(string framework, string inputFileName, string[] expectedOutput, IEnumerable<SettingItem<IProtection>> protections,
			string outputDirSuffix = "", Action<string> outputAction = null, SettingItem<IPacker> packer = null,
			Action<ProjectModule> projectModuleAction = null, Func<string, Task> postProcessAction = null,
			string seed = null, bool checkOutput = true) =>

			Run(framework, new[] { inputFileName }, expectedOutput, protections, outputDirSuffix, outputAction, packer,
				projectModuleAction, postProcessAction, seed, checkOutput);

		protected Task Run(string[] inputFileNames, string[] expectedOutput, SettingItem<IProtection> protection,
			string outputDirSuffix = "", Action<string> outputAction = null, SettingItem<IPacker> packer = null,
			Action<ProjectModule> projectModuleAction = null, Func<string, Task> postProcessAction = null,
			string seed = null, bool checkOutput = true) =>

			Run(null, inputFileNames, expectedOutput, protection, outputDirSuffix, outputAction, packer, projectModuleAction, postProcessAction, seed, checkOutput);

		protected Task Run(string framework, string[] inputFileNames, string[] expectedOutput, SettingItem<IProtection> protection,
			string outputDirSuffix = "", Action<string> outputAction = null, SettingItem<IPacker> packer = null,
			Action<ProjectModule> projectModuleAction = null, Func<string, Task> postProcessAction = null,
			string seed = null, bool checkOutput = true) {
			var protections = (protection is null) ? Enumerable.Empty<SettingItem<IProtection>>() : new[] { protection };
			return Run(framework, inputFileNames, expectedOutput, protections, outputDirSuffix, outputAction, packer, projectModuleAction, postProcessAction, seed, checkOutput);
		}

		protected Task Run(string[] inputFileNames, string[] expectedOutput, IEnumerable<SettingItem<IProtection>> protections,
			string outputDirSuffix = "", Action<string> outputAction = null, SettingItem<IPacker> packer = null,
			Action<ProjectModule> projectModuleAction = null, Func<string, Task> postProcessAction = null,
			string seed = null, bool checkOutput = true) =>

			Run(null, inputFileNames, expectedOutput, protections, outputDirSuffix, outputAction, packer, projectModuleAction, postProcessAction, seed, checkOutput);

		protected async Task Run(string framework, string[] inputFileNames, string[] expectedOutput, IEnumerable<SettingItem<IProtection>> protections,
			string outputDirSuffix = "", Action<string> outputAction = null, SettingItem<IPacker> packer = null,
			Action<ProjectModule> projectModuleAction = null, Func<string, Task> postProcessAction = null,
			string seed = null, bool checkOutput = true) {

			var baseDir = Path.Combine(Environment.CurrentDirectory, framework ?? "");
			var outputDirBaseName = "obfuscated";
			if (!string.IsNullOrWhiteSpace(framework))
				outputDirBaseName += "_" + framework;
			var outputDir = Path.Combine(baseDir, outputDirBaseName + outputDirSuffix);
			if (Directory.Exists(outputDir)) {
				Directory.Delete(outputDir, true);
			}

			string firstExecutable = inputFileNames.Select(GetFileName).First(n => n.EndsWith(".exe"));
			string entryInputFileName = Path.Combine(baseDir, firstExecutable);
			var entryOutputFileName = Path.Combine(outputDir, firstExecutable);
			var proj = new ConfuserProject {
				BaseDirectory = baseDir,
				OutputDirectory = outputDir,
				Packer = packer,
				Seed = seed
			};

			foreach (string name in inputFileNames) {
				var projectModule = new ProjectModule {
					Path = Path.Combine(baseDir, GetFileName(name)),
					IsExternal = IsExternal(name),
					SNKeyPath = Path.Combine(Environment.CurrentDirectory, "Confuser.Test.snk")
				};
				projectModuleAction?.Invoke(projectModule);
				proj.Add(projectModule);
			}

			var rule = new Rule();
			rule.AddRange(protections);
			if (rule.Count > 0)
				proj.Rules.Add(rule);

			var parameters = new ConfuserParameters {
				Project = proj,
				ConfigureLogging = builder => builder.AddProvider(new XunitLogger(outputHelper, outputAction))
			};

			await ConfuserEngine.Run(parameters);

			for (var index = 0; index < inputFileNames.Length; index++) {
				string name = GetFileName(inputFileNames[index]);
				string outputName = Path.Combine(outputDir, name);

				bool exists;
				if (index == 0) {
					Assert.True(File.Exists(outputName));
					exists = true;
				}
				else {
					exists = File.Exists(outputName);
				}

				if (exists) {
					// Check if output assemblies is obfuscated
					Assert.NotEqual(FileUtilities.ComputeFileChecksum(Path.Combine(baseDir, name)),
						FileUtilities.ComputeFileChecksum(outputName));
				}
				else if (IsExternal(inputFileNames[index])) {
					File.Copy(
						Path.Combine(baseDir, GetFileName(inputFileNames[index])),
						Path.Combine(outputDir, GetFileName(inputFileNames[index])));
				}
			}

			if (Path.GetExtension(entryOutputFileName) == ".exe") {
				var exitCode = await ProcessUtilities.ExecuteTestApplication(entryOutputFileName, async (stdout) => {
					if (checkOutput) {
						Assert.Equal("START", await stdout.ReadLineAsync());

						foreach (string line in expectedOutput) {
							Assert.Equal(line, await stdout.ReadLineAsync());
						}

						Assert.Equal("END", await stdout.ReadLineAsync());
					} else {
						await stdout.ReadToEndAsync();
					}
				}, outputHelper);

				Assert.Equal(42, exitCode);				
			}

			if (!(postProcessAction is null))
				await postProcessAction.Invoke(outputDir);
		}

		private static string GetFileName(string name) {
			if (IsExternal(name))
				return name.Substring(_externalPrefix.Length);
			return name;
		}

		private static bool IsExternal(string name) => name.StartsWith(_externalPrefix, StringComparison.OrdinalIgnoreCase);
	}
}
