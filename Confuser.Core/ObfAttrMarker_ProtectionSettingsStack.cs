using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Core {
	public partial class ObfAttrMarker {
		private sealed class ProtectionSettingsStack {
			private readonly IConfuserContext context;
			private readonly Stack<(ProtectionSettings Settings, IImmutableList<ProtectionSettingsInfo> Infos)> stack;
			private readonly IReadOnlyDictionary<string, IProtection> protections;
			private ProtectionSettings settings;

			private enum ApplyInfoType {
				CurrentInfoOnly,
				CurrentInfoInherits,
				ParentInfo
			}

			private struct PopHolder : IDisposable {
				private readonly ProtectionSettingsStack parent;

				public PopHolder(ProtectionSettingsStack parent) => this.parent = parent;

				public void Dispose() => parent.Pop();
			}

			private struct DummyDisposable : IDisposable {
				public void Dispose() {
				}
			}

			public ProtectionSettingsStack(IConfuserContext context, Dictionary<string, IProtection> protections) {
				this.context = context ?? throw new ArgumentNullException(nameof(context));
				stack = new Stack<(ProtectionSettings, IImmutableList<ProtectionSettingsInfo>)>();
				this.protections = protections ?? throw new ArgumentNullException(nameof(protections));
			}

			public ProtectionSettingsStack(ProtectionSettingsStack copy) {
				if (copy == null) throw new ArgumentNullException(nameof(copy));

				context = copy.context;
				stack = new Stack<(ProtectionSettings, IImmutableList<ProtectionSettingsInfo>)>(copy.stack);
				protections = copy.protections;
			}

			private void Pop() => settings = stack.Pop().Settings;


			public void Apply(IDnlibDef target) {
				var localSettings = new ProtectionSettings(settings);

				var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("core");

				if (stack.Count > 0) {
					foreach (var (_, stackInfos) in stack.Reverse())
						ApplyInfo(protections, target, localSettings, stackInfos, ApplyInfoType.ParentInfo, logger);
				}

				ProtectionParameters.SetParameters(context, target, localSettings);
			}

			public IDisposable Apply(IDnlibDef target, IEnumerable<ProtectionSettingsInfo> infos) {
				var localSettings = new ProtectionSettings(settings);

				var infoArray = infos.ToImmutableArray();

				var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("core");

				if (stack.Count > 0) {
					foreach (var (_, stackInfos) in stack.Reverse())
						ApplyInfo(protections, target, localSettings, stackInfos, ApplyInfoType.ParentInfo, logger);
				}

				IDisposable result;
				if (infoArray.Length != 0) {
					var originalSettings = settings;

					// the settings that would apply to members
					ApplyInfo(protections, target, localSettings, infoArray, ApplyInfoType.CurrentInfoInherits, logger);
					settings = new ProtectionSettings(localSettings);

					// the settings that would apply to itself
					ApplyInfo(protections, target, localSettings, infoArray, ApplyInfoType.CurrentInfoOnly, logger);
					stack.Push((originalSettings, infoArray));

					result = new PopHolder(this);
				}
				else
					result = new DummyDisposable();

				ProtectionParameters.SetParameters(context, target, localSettings);
				return result;
			}

			private static void ApplyInfo(IReadOnlyDictionary<string, IProtection> protections, IDnlibDef context,
				ProtectionSettings settings,
				IEnumerable<ProtectionSettingsInfo> infos, ApplyInfoType type, ILogger logger) {
				foreach (var info in infos) {
					if (info.Condition != null && !(bool)info.Condition.Evaluate(context))
						continue;

					if (info.Condition == null && info.Exclude) {
						if (type == ApplyInfoType.CurrentInfoOnly ||
							(type == ApplyInfoType.CurrentInfoInherits && info.ApplyToMember)) {
							settings.Clear();
						}
					}

					if (!string.IsNullOrEmpty(info.Settings)) {
						if ((type == ApplyInfoType.ParentInfo && info.ApplyToMember) ||
							type == ApplyInfoType.CurrentInfoOnly ||
							(type == ApplyInfoType.CurrentInfoInherits && info.Condition == null &&
							 info.ApplyToMember)) {
							ObfAttrParser.ParseProtection(protections, settings, info.Settings, logger);
						}
					}
				}
			}
		}
	}
}
