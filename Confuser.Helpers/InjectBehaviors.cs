using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Renamer.Services;
using dnlib.DotNet;

namespace Confuser.Helpers {
	public static class InjectBehaviors {

		/// <summary>
		/// This inject behavior will rename every method, field and type it encounters. It will also internalize all
		/// public elements and it will declare all dependency classes as nested private classes.
		/// </summary>
		/// <param name="targetType">The "main" type. Inside of this type, all the references will be stored.</param>
		/// <param name="nameService">The name service used to generate the random names.</param>
		/// <returns>The inject behavior with the described properties.</returns>
		/// <exception cref="ArgumentNullException">
		///   <paramref name="targetType"/> is <see langword="null"/>
		///   <br />- or -<br />
		///   <paramref name="nameService"/> is <see langword="null"/>
		/// </exception>
		public static IInjectBehavior RenameAndNestBehavior(IConfuserContext context, TypeDef targetType, INameService nameService) =>
			new RenameEverythingNestedPrivateDependenciesBehavior(context, targetType, nameService);

		private sealed class RenameEverythingNestedPrivateDependenciesBehavior : IInjectBehavior {
			private readonly IConfuserContext _context;
			private readonly TypeDef _targetType;
			private readonly INameService _nameService;

			internal RenameEverythingNestedPrivateDependenciesBehavior(IConfuserContext context, TypeDef targetType, INameService nameService) {
				_context = context ?? throw new ArgumentNullException(nameof(context));
				_targetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
				_nameService = nameService ?? throw new ArgumentNullException(nameof(nameService));
			}

			void IInjectBehavior.Process(TypeDef source, TypeDefUser injected) {
				if (source == null) throw new ArgumentNullException(nameof(source));
				if (injected == null) throw new ArgumentNullException(nameof(injected));
				
				_nameService.SetOriginalNamespace(_context, injected, injected.Namespace);
				_nameService.SetOriginalName(_context, injected, injected.Name);

				injected.Name = GetName(injected);
				injected.Namespace = null;
				if (injected.IsNested) {
					if (injected.IsNestedPublic)
						injected.Visibility = TypeAttributes.NestedAssembly;
				} else {
					injected.DeclaringType = _targetType;
					injected.Visibility = TypeAttributes.NestedPrivate;
				}
			}

			void IInjectBehavior.Process(MethodDef source, MethodDefUser injected) {
				if (source == null) throw new ArgumentNullException(nameof(source));
				if (injected == null) throw new ArgumentNullException(nameof(injected));

				_nameService.SetOriginalName(_context, injected, injected.Name);

				if (injected.IsPublic)
					injected.Access = MethodAttributes.Assembly;
				if (!injected.IsSpecialName)
					injected.Name = GetName(injected.Name);
			}

			void IInjectBehavior.Process(FieldDef source, FieldDefUser injected) {
				if (source == null) throw new ArgumentNullException(nameof(source));
				if (injected == null) throw new ArgumentNullException(nameof(injected));

				_nameService.SetOriginalName(_context, injected, injected.Name);

				if (injected.IsPublic)
					injected.Access = FieldAttributes.Assembly;
				if (!injected.IsSpecialName)
					injected.Name = GetName(injected.Name);
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
	}
}
