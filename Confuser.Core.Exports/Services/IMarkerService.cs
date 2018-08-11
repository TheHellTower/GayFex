using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;

namespace Confuser.Core.Services
{
	/// <summary>
	///     Provides methods to access the obfuscation marker.
	/// </summary>
	public interface IMarkerService {
		/// <summary>
		///     Marks the helper member.
		/// </summary>
		/// <param name="context">The working confuser context.</param>
		/// <param name="member">The helper member.</param>
		/// <param name="parentComp">The parent component.</param>
		/// <exception cref="System.ArgumentException"><paramref name="member" /> is a <see cref="ModuleDef" />.</exception>
		/// <exception cref="System.ArgumentNullException"><paramref name="member" /> is <c>null</c>.</exception>
		void Mark(IConfuserContext context, IDnlibDef member, IConfuserComponent parentComp);

		/// <summary>
		///     Determines whether the specified definition is marked.
		/// </summary>
		/// <param name="context">The working confuser context.</param>
		/// <param name="def">The definition.</param>
		/// <returns><c>true</c> if the specified definition is marked; otherwise, <c>false</c>.</returns>
		bool IsMarked(IConfuserContext context, IDnlibDef def);

		/// <summary>
		///     Gets the parent component of the specified helper.
		/// </summary>
		/// <param name="def">The helper definition.</param>
		/// <returns>The parent component of the helper, or <c>null</c> if the specified definition is not a helper.</returns>
		IConfuserComponent GetHelperParent(IDnlibDef def);

		StrongNameKey GetStrongNameKey(IConfuserContext context, ModuleDefMD module);
	}
}
