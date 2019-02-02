using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Runtime.CompilerServices;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Confuser.Core {
	public class ObfAttrParserTest {
		private ITestOutputHelper OutputHelper { get; }

		public ObfAttrParserTest(ITestOutputHelper outputHelper) =>
			OutputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Fact]
		[Trait("Category", "Core")]
		[Trait("Core", "pattern parser")]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void TestEmptyPattern() {
			var logger = new XunitLogger(OutputHelper);
			var protections = GetProtections(DiscoverPlugIns(logger));

			var settings = new Dictionary<IConfuserComponent, IDictionary<string, string>>();
			var newSettings = ObfAttrParser.ParseProtection(protections, settings, "", logger);

			Assert.Same(settings, newSettings);
			Assert.Empty(settings);
		}

		[Theory]
		[MemberData(nameof(PresetData))]
		[Trait("Category", "Core")]
		[Trait("Core", "pattern parser")]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void TestPresetPattern(string preset) {
			var logger = new XunitLogger(OutputHelper);
			var protections = GetProtections(DiscoverPlugIns(logger));

			var settings = new Dictionary<IConfuserComponent, IDictionary<string, string>>();

			var expression = $"preset({preset})";
			var newSettings = ObfAttrParser.ParseProtection(protections, settings, expression, logger);

			Assert.Same(settings, newSettings);
			Assert.True(Enum.TryParse<ProtectionPreset>(preset, out var presetEnum));

			foreach (var protection in protections.Values) {
				if (protection.Preset != ProtectionPreset.None && protection.Preset <= presetEnum) {
					Assert.Empty(Assert.Contains(protection, newSettings));
				}
				else {
					Assert.DoesNotContain(protection, newSettings);
				}
			}
		}

		[Theory]
		[MemberData(nameof(ProtectionIdData))]
		[Trait("Category", "Core")]
		[Trait("Core", "pattern parser")]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void TestEnableProtection(string protectionId) =>
			TestDisableEnableProtection(protectionId, true);

		[Theory]
		[MemberData(nameof(ProtectionIdData))]
		[Trait("Category", "Core")]
		[Trait("Core", "pattern parser")]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void TestDisableProtection(string protectionId) =>
			TestDisableEnableProtection(protectionId, false);

		private void TestDisableEnableProtection(string protectionId, bool enable) {
			var logger = new XunitLogger(OutputHelper);
			var protections = GetProtections(DiscoverPlugIns(logger));

			var selectedProtection = protections[protectionId];

			{
				// Enable or disable the entire component.
				IDictionary<IConfuserComponent, IDictionary<string, string>> settings;
				if (enable)
					settings = new Dictionary<IConfuserComponent, IDictionary<string, string>>();
				else
					settings = protections.ToDictionary(kvp => (IConfuserComponent)kvp.Value,
						kvp => (IDictionary<string, string>)new Dictionary<string, string>());

				var expression = (enable ? "+" : "-") + protectionId;
				var newSettings = ObfAttrParser.ParseProtection(protections, settings, expression, logger);
				logger.CheckErrors();

				Assert.Same(settings, newSettings);

				foreach (var protection in protections.Values) {
					if ((protection == selectedProtection) == enable)
						Assert.Empty(Assert.Contains(protection, newSettings));
					else
						Assert.DoesNotContain(protection, newSettings);
				}
			}

			// Enable or disable the individual options.
			if (selectedProtection.Parameters.Any()) {
				// Now we are creating sample values for every single parameter.
				var sampleValues = CreateSampleValues(selectedProtection);

				foreach (var selectedParameters in GetEveryPermutation(sampleValues)) {
					IDictionary<IConfuserComponent, IDictionary<string, string>> settings;
					if (enable)
						settings = new Dictionary<IConfuserComponent, IDictionary<string, string>>();
					else
						settings = protections.ToDictionary(
							kvp => (IConfuserComponent)kvp.Value,
							kvp => {
								IDictionary<string, string> result;
								if (kvp.Value == selectedProtection)
									result = sampleValues.ToDictionary(sv => sv.Name, sv => sv.Value);
								else
									result = new Dictionary<string, string>();
								return result;
							});

					var paramKvp = selectedParameters.Select(sp => sp.Name + "='" + sp.Value.Replace("'", "\'", StringComparison.Ordinal) + "'");
					var expression = (enable ? "+" : "-") + protectionId + "(" + string.Join(';', paramKvp) + ")";

					var newSettings = ObfAttrParser.ParseProtection(protections, settings, expression, logger);
					logger.CheckErrors();

					Assert.Same(settings, newSettings);

					foreach (var protection in protections.Values) {
						if (protection == selectedProtection) {
							var appliedParameters = Assert.Contains(protection, newSettings);
							foreach (var selectedParam in selectedParameters) {
								if (enable) {
									Assert.Equal(selectedParam.Value, Assert.Contains(selectedParam.Name, appliedParameters));
									appliedParameters.Remove(selectedParam.Name);
								}
								else
									Assert.DoesNotContain(selectedParam.Name, appliedParameters);
							}
							if (enable)
								Assert.Empty(appliedParameters);
						}
						else if (enable)
							Assert.DoesNotContain(protection, newSettings);
						else
							Assert.Empty(Assert.Contains(protection, newSettings));
					}
				}
			}
		}

		private static IList<(string Name, string Value)> CreateSampleValues(IProtection selectedProtection) {
			var sampleValues = new List<(string Name, string Value)>();
			var rnd = new Random();
			foreach (var param in selectedProtection.Parameters.Values) {
				if (param is IProtectionParameter<int> intParam)
					sampleValues.Add((intParam.Name, intParam.Serialize(rnd.Next())));
				else if (param is IProtectionParameter<uint> uintParam)
					sampleValues.Add((uintParam.Name, uintParam.Serialize((uint)rnd.Next())));
				else if (param is IProtectionParameter<double> doubleParam)
					sampleValues.Add((doubleParam.Name, doubleParam.Serialize(rnd.NextDouble())));
				else if (param is IProtectionParameter<bool> boolParam)
					sampleValues.Add((boolParam.Name, boolParam.Serialize(rnd.NextDouble() > 0.5)));
			}

			return sampleValues;
		}

		private static IEnumerable<IList<T>> GetEveryPermutation<T>(IList<T> values) {
			if (!values.Any()) yield break;

			var variations = 2 << (values.Count - 1);
			for (int i = 1; i < variations; i++) {
				var permutation = new List<T>();
				for (int j = 0; j < values.Count; j++) {
					if ((i & (1 << j)) != 0)
						permutation.Add(values[j]);
				}
				yield return permutation;
			}
		}

		private static CompositionContainer DiscoverPlugIns(ILogger logger) =>
			PluginDiscovery.Instance.GetPlugins(new ConfuserProject(), logger);

		private static IReadOnlyDictionary<string, IProtection> GetProtections(CompositionContainer container) =>
			container.GetExports<IProtection, IProtectionMetadata>().ToDictionary(p => p.Metadata.MarkerId ?? p.Metadata.Id, p => p.Value);

		public static IEnumerable<object[]> PresetData() {
			foreach (var preset in Enum.GetValues(typeof(ProtectionPreset))) {
				yield return new object[] { Enum.GetName(typeof(ProtectionPreset), preset) };
			}
		}

		public static IEnumerable<object[]> ProtectionIdData() {
			var protections = GetProtections(DiscoverPlugIns(NullLogger.Instance));
			foreach (var protectionId in protections.Keys.OrderBy(k => k, StringComparer.Ordinal)) {
				yield return new object[] { protectionId };
			}
		}
	}
}
