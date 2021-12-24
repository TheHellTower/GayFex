using System;
using System.Collections.Generic;
using Confuser.Core;
using Confuser.Core.Services;
using dnlib.DotNet;

namespace Confuser.Renamer.Services {
	public interface INameService {
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

		void SetParam<T>(IConfuserContext context, IDnlibDef def, IProtectionParameter<T> protectionParameter, T value);
		T GetParam<T>(IConfuserContext context, IDnlibDef def, IProtectionParameter<T> protectionParameter);

		RenameMode GetRenameMode(IConfuserContext context, object obj);
		void SetRenameMode(IConfuserContext context, object obj, RenameMode val);
		void ReduceRenameMode(IConfuserContext context, object obj, RenameMode val);

		string ObfuscateName(IConfuserContext context, IDnlibDef name, RenameMode mode);
		string RandomName();
		string RandomName(RenameMode mode);

		void RegisterRenamer(IRenamer renamer);
		T FindRenamer<T>();
		void AddReference<T>(IConfuserContext context, T obj, INameReference<T> reference);
		IList<INameReference> GetReferences(IConfuserContext context, object obj);

		void StoreNames(IConfuserContext context, IDnlibDef obj);
		void SetNormalizedName(IConfuserContext context, IDnlibDef obj, string name);
		string GetDisplayName(IConfuserContext context, IDnlibDef obj);
		string GetNormalizedName(IConfuserContext context, IDnlibDef obj);

		bool IsRenamed(IConfuserContext context, IDnlibDef def);
		void SetIsRenamed(IConfuserContext context, IDnlibDef def);

		void MarkHelper(IConfuserContext context, IDnlibDef def, IMarkerService marker, IConfuserComponent parentComp);
	}
}
