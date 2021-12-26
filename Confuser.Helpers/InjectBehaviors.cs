using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Confuser.Analysis;
using Confuser.Analysis.Services;
using Confuser.Core;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Helpers {
	public static class InjectBehaviors {
		/// <summary>
		/// This inject behavior will rename every method, field and type it encounters. It will also internalize all
		/// public elements and it will declare all dependency classes as nested private classes.
		/// </summary>
		/// <param name="targetType">The "main" type. Inside of this type, all the references will be stored.</param>
		/// <returns>The inject behavior with the described properties.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="targetType"/> is <see langword="null"/></exception>
		/// <exception cref="InvalidOperationException"><see cref="INameService"/> is not registered</exception>
		public static IInjectBehavior RenameAndNestBehavior(IConfuserContext context, TypeDef targetType) =>
			new RenameEverythingNestedPrivateDependenciesBehavior(context, targetType);

		public static IInjectBehavior RenameAndInternalizeBehavior(IConfuserContext context) =>
			new RenameEverythingInternalDependenciesBehavior(context);

		public static IInjectBehavior RenameBehavior(IConfuserContext context) =>
			new RenameEverythingBehavior(context);

		private class RenameEverythingBehavior : IInjectBehavior {
			private readonly IConfuserContext _context;
			private readonly INameService _nameService;
			private IAnalysisService AnalysisService { get; }

			internal RenameEverythingBehavior(IConfuserContext context) {
				_context = context ?? throw new ArgumentNullException(nameof(context));
				AnalysisService = context.Registry.GetRequiredService<IAnalysisService>();
				_nameService = context.Registry.GetRequiredService<INameService>();
			}

			public virtual void Process(TypeDef source, TypeDefUser injected, Importer importer) {
				if (source == null) throw new ArgumentNullException(nameof(source));
				if (injected == null) throw new ArgumentNullException(nameof(injected));

				_nameService.StoreNames(_context, injected);

				injected.Name = GetName(injected);
				injected.Namespace = null;

				// There is no need for this to be renamed again.
				_nameService.SetCanRename(_context, injected, false);
			}

			public virtual void Process(MethodDef source, MethodDefUser injected, Importer importer) {
				if (source == null) throw new ArgumentNullException(nameof(source));
				if (injected == null) throw new ArgumentNullException(nameof(injected));

				_nameService.StoreNames(_context, injected);

				bool renamingAllowed = true;

				if (injected.IsSpecialName)
					renamingAllowed = false;

				if (renamingAllowed) {
					var vTable = AnalysisService.GetVTable(source.DeclaringType);
					foreach (var slot in vTable.FindSlots(source)) {
						var overrideSource = slot.Overrides.MethodDef;
						if (overrideSource.DeclaringType.IsInterface) {
							// override of an interface. Renaming is possible, but we may need an method override declaration.
							var overrideDecl = new MethodOverride(injected, (IMethodDefOrRef)importer.Import(overrideSource));
							if (!source.Overrides.Any(o => MethodEqualityComparer.CompareDeclaringTypes.Equals(o.MethodDeclaration, overrideDecl.MethodDeclaration))) {
								source.Overrides.Add(overrideDecl);
							}
						}
						else {
							renamingAllowed = false;
						}
					}
				}


				if (renamingAllowed)
					injected.Name = GetName(injected.Name);

				// There is no need for this to be renamed again.
				_nameService.SetCanRename(_context, injected, false);
			}

			public virtual void Process(FieldDef source, FieldDefUser injected, Importer importer) {
				if (source == null) throw new ArgumentNullException(nameof(source));
				if (injected == null) throw new ArgumentNullException(nameof(injected));

				_nameService.StoreNames(_context, injected);

				if (!injected.IsSpecialName)
					injected.Name = GetName(injected.Name);

				// There is no need for this to be renamed again.
				_nameService.SetCanRename(_context, injected, false);
			}

			public virtual void Process(EventDef source, EventDefUser injected, Importer importer) {
				if (source == null) throw new ArgumentNullException(nameof(source));
				if (injected == null) throw new ArgumentNullException(nameof(injected));

				_nameService.StoreNames(_context, injected);

				if (!injected.IsSpecialName)
					injected.Name = GetName(injected.Name);

				// There is no need for this to be renamed again.
				_nameService.SetCanRename(_context, injected, false);
			}

			public virtual void Process(PropertyDef source, PropertyDefUser injected, Importer importer) {
				if (source == null) throw new ArgumentNullException(nameof(source));
				if (injected == null) throw new ArgumentNullException(nameof(injected));

				_nameService.StoreNames(_context, injected);

				if (!injected.IsSpecialName)
					injected.Name = GetName(injected.Name);

				// There is no need for this to be renamed again.
				_nameService.SetCanRename(_context, injected, false);
			}

			private string GetName(TypeDef type) {
				var nameBuilder = new StringBuilder();
				nameBuilder.Append(type.Namespace);
				nameBuilder.Append('_');
				nameBuilder.Append(type.Name);
				var declaringType = type.DeclaringType;
				if (declaringType != null) {
					nameBuilder.Insert(0, '+');
					nameBuilder.Insert(0, declaringType.Name);
				}

				nameBuilder.Replace('.', '_').Replace('/', '_');
				return GetName(nameBuilder.ToString());
			}

			private string GetName(string originalName) {
				//return _nameService.ObfuscateName(originalName.Replace('.', '_'), Renamer.RenameMode.Debug);
				return _nameService.RandomName(Renamer.RenameMode.Letters);
			}
		}

		private class RenameEverythingInternalDependenciesBehavior : RenameEverythingBehavior {
			internal RenameEverythingInternalDependenciesBehavior(IConfuserContext context) : base(context) {
			}

			public override void Process(TypeDef source, TypeDefUser injected, Importer importer) {
				base.Process(source, injected, importer);

				if (injected.IsNested) {
					if (injected.IsNestedPublic)
						injected.Visibility = TypeAttributes.NestedAssembly;
				}
				else if (injected.IsPublic)
					injected.Visibility = TypeAttributes.NotPublic;
			}

			public override void Process(MethodDef source, MethodDefUser injected, Importer importer) {
				base.Process(source, injected, importer);

				if (!injected.HasOverrides && injected.IsPublic && !injected.IsOverride())
					injected.Access = MethodAttributes.Assembly;
			}

			public override void Process(FieldDef source, FieldDefUser injected, Importer importer) {
				base.Process(source, injected, importer);

				if (injected.IsPublic)
					injected.Access = FieldAttributes.Assembly;
			}
		}

		private class RenameEverythingNestedPrivateDependenciesBehavior : RenameEverythingInternalDependenciesBehavior {
			private readonly TypeDef _targetType;

			internal RenameEverythingNestedPrivateDependenciesBehavior(IConfuserContext context, TypeDef targetType)
				: base(context) =>
				_targetType = targetType ?? throw new ArgumentNullException(nameof(targetType));

			public override void Process(TypeDef source, TypeDefUser injected, Importer importer) {
				base.Process(source, injected, importer);

				if (!injected.IsNested) {
					var declaringType = (TypeDef)importer.Import(_targetType);
					if (declaringType != injected) {
						injected.DeclaringType = declaringType;
						injected.Visibility = TypeAttributes.NestedPrivate;
					}
				}
			}
		}
	}
}
