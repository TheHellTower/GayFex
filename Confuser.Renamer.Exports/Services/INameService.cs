using System;
using System.Collections.Generic;
using Confuser.Core;
using Confuser.Core.Services;
using dnlib.DotNet;

namespace Confuser.Renamer.Services
{	public interface INameService {
		void Analyze(IConfuserContext context, IDnlibDef def);

		/// <summary>
		///   Check if a specified definition is allowed to be renamed.
		/// </summary>
		/// <param name="context">The context to use</param>
		/// <param name="def">The definition that is allowed to be renamed, or not.</param>
		/// <exception cref="ArgumentNullException">
		///   <paramref name="context"/> is <see langword="null" />
		/// </exception>
		/// <returns>
		///   <see langword="true"/> in case <paramref name="def"/> is allowed to be renamed, or 
		///   <see langword="false"/> in case renaming the definition was forbidden using
		///   <see cref="SetCanRename(IConfuserContext, IDnlibDef, bool)"/>
		///   or in case <paramref name="def"/> is <see langword="null"/>.
		/// </returns>
		bool CanRename(IConfuserContext context, IDnlibDef def);

		/// <summary>
		///   Set if a specified definition is allowed to be renamed or not.
		/// </summary>
		/// <param name="context">The context to use</param>
		/// <param name="def">The definition that is allowed to be renamed, or not.</param>
		/// <param name="val"><see langword="true" /> in case the definition is allowed to be renamed.</param>
		/// <exception cref="ArgumentNullException">
		///   <paramref name="context"/> is <see langword="null" />
		///   <br/>- or -<br/>
		///   <paramref name="def"/> is <see langword="null" />
		/// </exception>
		void SetCanRename(IConfuserContext context, IDnlibDef def, bool val);

		void SetParam(IConfuserContext context, IDnlibDef def, string name, string value);
		string GetParam(IConfuserContext context, IDnlibDef def, string name);

		RenameMode GetRenameMode(IConfuserContext context, object obj);
		void SetRenameMode(IConfuserContext context, object obj, RenameMode val);
		void ReduceRenameMode(IConfuserContext context, object obj, RenameMode val);

		string ObfuscateName(string name, RenameMode mode);
		string RandomName();
		string RandomName(RenameMode mode);

		void RegisterRenamer(IRenamer renamer);
		T FindRenamer<T>();
		void AddReference<T>(IConfuserContext context, T obj, INameReference<T> reference);

		void SetOriginalName(IConfuserContext context, object obj, string name);
		void SetOriginalNamespace(IConfuserContext context, object obj, string ns);

		void MarkHelper(IConfuserContext context, IDnlibDef def, IMarkerService marker, IConfuserComponent parentComp);

		IReadOnlyCollection<KeyValuePair<string, string>> GetNameMap();
	}
}
