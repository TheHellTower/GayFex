using System;
using System.Linq;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using Confuser.Renamer;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	internal sealed class AntiDebugProtectionPhase : IProtectionPhase {
		public AntiDebugProtectionPhase(AntiDebugProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public AntiDebugProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Modules;

		public string Name => "Anti-debug injection";

		public bool ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			var rt = context.Registry.GetRequiredService<IRuntimeService>();
			var marker = context.Registry.GetRequiredService<IMarkerService>();
			var name = context.Registry.GetService<INameService>();

			foreach (var module in parameters.Targets.OfType<ModuleDef>()) {
				var mode = parameters.GetParameter(context, module, Parent.Parameters.Mode);

				TypeDef rtType;
				TypeDef attr = null;
				const string attrName = "System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute";
				switch (mode) {
				case AntiDebugMode.Safe:
					rtType = rt.GetRuntimeType("Confuser.Runtime.AntiDebugSafe");
					break;
				case AntiDebugMode.Win32:
					rtType = rt.GetRuntimeType("Confuser.Runtime.AntiDebugWin32");
					break;
				case AntiDebugMode.Antinet:
					rtType = rt.GetRuntimeType("Confuser.Runtime.AntiDebugAntinet");

					attr = rt.GetRuntimeType(attrName);
					module.Types.Add(attr = InjectHelper.Inject(attr, module));
					foreach (var member in attr.FindDefinitions()) {
						marker.Mark(context, member, Parent);
						name?.Analyze(context, member);
					}
					name?.SetCanRename(context, attr, false);
					break;
				default:
					throw new UnreachableException();
				}

				var members = InjectHelper.Inject(rtType, module.GlobalType, module);

				var cctor = module.GlobalType.FindStaticConstructor();
				var init = (MethodDef)members.Single(method => method.Name == "Initialize");
				cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, init));

				foreach (var member in members) {
					marker.Mark(context, member, Parent);
					name?.Analyze(context, member);

					bool ren = true;
					if (member is MethodDef method) {
						if (method.Access == MethodAttributes.Public)
							method.Access = MethodAttributes.Assembly;
						if (!method.IsConstructor)
							method.IsSpecialName = false;
						else
							ren = false;

						var ca = method.CustomAttributes.Find(attrName);
						if (ca != null)
							ca.Constructor = attr.FindMethod(".ctor");
					}
					else if (member is FieldDef field) {
						if (field.Access == FieldAttributes.Public)
							field.Access = FieldAttributes.Assembly;
						if (field.IsLiteral) {
							field.DeclaringType.Fields.Remove(field);
							continue;
						}
					}
					if (ren && name != null) {
						member.Name = name.ObfuscateName(module, member.Name, RenameMode.Unicode);
						name.SetCanRename(context, member, false);
					}
				}
			}
		}
	}
}
