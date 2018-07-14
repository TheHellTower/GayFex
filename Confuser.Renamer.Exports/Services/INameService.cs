using System;
using System.Collections.Generic;
using Confuser.Core;
using Confuser.Core.Services;
using dnlib.DotNet;

namespace Confuser.Renamer.Services
{	public interface INameService {
		void Analyze(IConfuserContext context, IDnlibDef def);

		bool CanRename(IConfuserContext context, object obj);
		void SetCanRename(IConfuserContext context, object obj, bool val);

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
