using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Renamer.Services {
	public static class NameServiceExtensions {
		/// <summary>
		///   Check if a specified definition is allowed to be renamed.
		/// </summary>
		/// <param name="service">The used service.</param>
		/// <param name="context">The context to use</param>
		/// <param name="memberForwarded">The definition that is allowed to be renamed, or not.</param>
		/// <exception cref="ArgumentNullException">
		///   <paramref name="service"/> is <see langword="null" />
		///   <br/>- or -<br/>
		///   <paramref name="context"/> is <see langword="null" />
		/// </exception>
		/// <exception cref="NotSupportedException">
		///   <paramref name="memberForwarded"/> is neither a <see cref="MethodDef"/> nor a <see cref="FieldDef"/>
		/// </exception>
		/// <returns>
		///   <see langword="true"/> in case <paramref name="def"/> is allowed to be renamed, or 
		///   <see langword="false"/> in case renaming the definition was forbidden using
		///   <see cref="SetCanRename(INameService, IConfuserContext, IDnlibDef, bool)"/>
		///   or in case <paramref name="memberForwarded"/> is <see langword="null"/>.
		/// </returns>
		public static bool CanRename(this INameService service, IConfuserContext context,
			IMemberForwarded memberForwarded) {
			if (service == null) throw new ArgumentNullException(nameof(service));
			if (context == null) throw new ArgumentNullException(nameof(context));

			if (memberForwarded == null) return false;

			return service.CanRename(context, CheckImplementation(memberForwarded));
		}

		/// <summary>
		///   Set if a specified definition is allowed to be renamed or not.
		/// </summary>
		/// <param name="service">The used service.</param>
		/// <param name="context">The context to use</param>
		/// <param name="memberForwarded">The definition that is allowed to be renamed, or not.</param>
		/// <param name="val"><see langword="true" /> in case the definition is allowed to be renamed.</param>
		/// <exception cref="ArgumentNullException">
		///   <paramref name="service"/> is <see langword="null" />
		///   <br/>- or -<br/>
		///   <paramref name="context"/> is <see langword="null" />
		///   <br/>- or -<br/>
		///   <paramref name="memberForwarded"/> is <see langword="null" />
		/// </exception>
		/// <exception cref="NotSupportedException">
		///   <paramref name="memberForwarded"/> is neither a <see cref="MethodDef"/> nor a <see cref="FieldDef"/>
		/// </exception>
		public static void SetCanRename(this INameService service, IConfuserContext context,
			IMemberForwarded memberForwarded, bool val) {
			if (service == null) throw new ArgumentNullException(nameof(service));
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (memberForwarded == null) throw new ArgumentNullException(nameof(memberForwarded));

			service.SetCanRename(context, CheckImplementation(memberForwarded), val);
		}

		private static IDnlibDef CheckImplementation(IMemberForwarded memberForwarded) {
			Debug.Assert(memberForwarded != null, "memberForwarded != null");

			var methodDef = (memberForwarded as MethodDef);
			if (methodDef != null) return methodDef;

			var fieldDef = (memberForwarded as FieldDef);
			if (fieldDef != null) return fieldDef;

			throw new NotSupportedException("Unknown implementation of IMemberForwarded: " +
			                                memberForwarded.GetType().FullName);
		}
	}
}
