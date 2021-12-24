using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml;
using Confuser.CLI.Properties;
using Confuser.Core;
using Confuser.Core.Project;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace Confuser.CLI {
	internal static class Program {
		internal static int Main(string[] args) {
			var cmd = new CommandLineApplication {
				AllowArgumentSeparator = true,
				ExtendedHelpText = Resources.ExtendedHelpText
			};
			var noPause = cmd.Option(
				"-n|-nopause",
				Resources.OptionNoPauseDescription,
				CommandOptionType.NoValue);
			var outDir = cmd.Option(
				"-o|-out <path>",
				Resources.OptionOutDirDescription,
				CommandOptionType.SingleValue);
			var probePaths = cmd.Option(
				"-probe <paths>",
				Resources.OptionProbeDescription,
				CommandOptionType.MultipleValue);
			var plugins = cmd.Option(
				"-plugin <paths>",
				Resources.OptionPluginDescription,
				CommandOptionType.MultipleValue);
			var debug = cmd.Option(
				"-debug",
				Resources.OptionDebugDescription,
				CommandOptionType.NoValue);
			var verbosity = cmd.Option(
				"-v|-verbosity <verbosity>",
				Resources.OptionVerbosityDescription,
				CommandOptionType.SingleValue);
			var files = cmd.Argument(
				"files",
				Resources.ArgumentFilesDescription,
				true);

			cmd.HelpOption("-?|-h|-help");
			cmd.VersionOption("-version", Resources.VersionShort,
				string.Format(Resources.Culture, Resources.VersionLong, ThisAssembly.AssemblyVersion));

			cmd.OnExecute(async () => {
				if (files.Values.Count == 0) {
					cmd.ShowHelp();
					return -1;
				}

				var parameters = new ConfuserParameters();

				if (files.Values.Count == 1 && Path.GetExtension(files.Values[0]) == ".crproj") {
					try {
						var projFile = files.Values[0];
						var proj = LoadConfuserProject(projFile);
						parameters.Project = proj;
					}
					catch (Exception ex) {
						WriteLineWithColor(ConsoleColor.Red,
							string.Format(Resources.Culture, Resources.ErrorLoadingProjectFailed, ex.ToString()));
						return -1;
					}
				}
				else {
					if (string.IsNullOrEmpty(outDir.Value())) {
						WriteLineWithColor(ConsoleColor.Red, Resources.ErrorNoOutputSpecified);
						cmd.ShowHelp();
						return -1;
					}

					var proj = new ConfuserProject();
					var templateModules = new List<ProjectModule>();

					if (Path.GetExtension(files.Values.Last()) == ".crproj") {
						LoadTemplateProject(files.Values.Last(), proj, templateModules);
						files.Values.RemoveAt(files.Values.Count - 1);
					}

					// Generate a ConfuserProject for input modules
					// Assuming first file = main module
					proj.BaseDirectory = Path.GetDirectoryName(files.Values.First());
					if (string.IsNullOrWhiteSpace(proj.BaseDirectory)) {
						WriteLineWithColor(ConsoleColor.Red, "Failed to identify base directory for main assembly.");
						cmd.ShowHelp();
						return -1;
					}

					foreach (var input in files.Values) {
						string modulePath = input;
						if (modulePath.StartsWith(proj.BaseDirectory, StringComparison.OrdinalIgnoreCase)) {
							modulePath = modulePath.Substring(proj.BaseDirectory.Length + 1);
						}

						if (TryMatchTemplateProject(templateModules, proj.BaseDirectory, modulePath, out var matchedModule))
							proj.Add(matchedModule);
						else
							proj.Add(new ProjectModule { Path = modulePath });
					}

					proj.OutputDirectory = outDir.Value();

					foreach (var path in probePaths.Values)
						proj.ProbePaths.Add(path);
					foreach (var path in plugins.Values)
						proj.PluginPaths.Add(path);
					proj.Debug = debug.HasValue();
					parameters.Project = proj;
				}

				parameters.ConfigureLogging = builder =>
					builder.AddConsole(b => b.IncludeScopes = false).SetMinimumLevel(GetLogLevel(verbosity));

				int retVal = await RunProject(parameters);

				// ReSharper disable once InvertIf
				if (NeedPause() && !noPause.HasValue()) {
					Console.WriteLine(Resources.PressAnyKeyToContinue);
					Console.ReadKey(true);
				}

				return retVal;
			});

			var originalColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.White;

			string originalTitle = null;
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				originalTitle = Console.Title;
			Console.Title = Resources.ConsoleTitle;

			try {
				return cmd.Execute(args);
			}
			catch (Exception ex) {
				WriteLineWithColor(ConsoleColor.Red,
					string.Format(Resources.Culture, Resources.ErrorUnexpected, ex.ToString()));

				// ReSharper disable once InvertIf
				if (NeedPause()) {
					Console.WriteLine(Resources.PressAnyKeyToContinue);
					Console.ReadKey(true);
				}

				return -1;
			}
			finally {
				Console.ForegroundColor = originalColor;
				Console.Title = originalTitle;
			}
		}

		private static bool TryMatchTemplateProject(List<ProjectModule> templateModules, string baseDirectory, string modulePath, out ProjectModule matchedModule) {
			var matchedToTemplate = false;
			matchedModule = null;

			foreach (var templateModule in templateModules) {
				var templatePath = templateModule.Path;
				if (templatePath.StartsWith(@".\", StringComparison.Ordinal))
					templatePath = templatePath.Substring(2);

				if (modulePath.Equals(templatePath, StringComparison.OrdinalIgnoreCase))
					matchedToTemplate = true;

				if (modulePath.Equals(Path.Combine(baseDirectory, templatePath), StringComparison.OrdinalIgnoreCase))
					matchedToTemplate = true;

				if (matchedToTemplate)
					matchedModule = templateModule;
			}

			return matchedToTemplate;
		}

		private static void LoadTemplateProject(string templatePath, ConfuserProject proj, List<ProjectModule> templateModules) {
			var templateProj = new ConfuserProject();
			var xmlDoc = new XmlDocument();
			xmlDoc.Load(templatePath);
			templateProj.Load(xmlDoc);

			foreach (var rule in templateProj.Rules)
				proj.Rules.Add(rule);

			proj.Packer = templateProj.Packer;

			foreach (string pluginPath in templateProj.PluginPaths)
				proj.PluginPaths.Add(pluginPath);

			foreach (string probePath in templateProj.ProbePaths)
				proj.ProbePaths.Add(probePath);

			foreach (var templateModule in templateProj)
				if (templateModule.IsExternal)
					proj.Add(templateModule);
				else
					templateModules.Add(templateModule);
		}

		private static ConfuserProject LoadConfuserProject(string projFile) {
			var proj = new ConfuserProject();
			var xmlDoc = new XmlDocument();
			xmlDoc.Load(projFile);
			proj.Load(xmlDoc);
			proj.BaseDirectory = Path.Combine(Path.GetDirectoryName(projFile), proj.BaseDirectory);
			return proj;
		}

		private static async Task<int> RunProject(ConfuserParameters parameters) {
			Console.Title = Resources.ConsoleTitleRunning;
			return (await ConfuserEngine.Run(parameters)) ? 0 : -1;
		}

		private static LogLevel GetLogLevel(CommandOption verbosityOption) {
			if (!verbosityOption.HasValue()) return LogLevel.Information;

			switch (verbosityOption.Value()) {
				case "t":
				case "trace":
					return LogLevel.Trace;
				case "d":
				case "debug":
					return LogLevel.Debug;
				case "i":
				case "info":
					return LogLevel.Information;
				case "w":
				case "warn":
				case "warning":
					return LogLevel.Warning;
				case "e":
				case "error":
					return LogLevel.Error;
				case "c":
				case "critical":
					return LogLevel.Critical;
				default:
					return LogLevel.Information;
			}
		}

		private static bool NeedPause() =>
			Debugger.IsAttached || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROMPT"));

		private static void WriteLineWithColor(ConsoleColor color, string txt) {
			var original = Console.ForegroundColor;
			try {
				Console.ForegroundColor = color;
				Console.WriteLine(txt);
			}
			finally {
				Console.ForegroundColor = original;
			}
		}
	}
}
