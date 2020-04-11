using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	internal sealed class RequiredPrefixReference<T> : INameReference<T> where T : class, IDnlibDef {
		T Def { get; }
		string Prefix  { get; }

		internal RequiredPrefixReference(T def, string prefix) {
			Def = def ?? throw new ArgumentNullException(nameof(def));
			Prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));
			if (prefix.Length < 0) throw new ArgumentException("Prefix must not be empty.", nameof(prefix));
		}

		/// <inheritdoc />
		public bool UpdateNameReference(ConfuserContext context, INameService service) {
			if (Def.Name.StartsWith(Prefix, StringComparison.Ordinal)) return false;

			Def.Name = Prefix + Def.Name;
			return true;
		}

		/// <inheritdoc />
		public bool ShouldCancelRename() => false;
	}
}
