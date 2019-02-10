using System;
using System.Collections.Generic;
using dnlib.DotNet;

namespace Confuser.Core.Services {
	internal class MarkerService : IMarkerService {
		readonly Marker marker;
		readonly Dictionary<IDnlibDef, IConfuserComponent> helperParents;

		/// <summary>
		///     Initializes a new instance of the <see cref="MarkerService" /> class.
		/// </summary>
		/// <param name="marker">The marker.</param>
		public MarkerService(Marker marker) {
			this.marker = marker;
			helperParents = new Dictionary<IDnlibDef, IConfuserComponent>();
		}

		/// <inheritdoc />
		public void Mark(IConfuserContext context, IDnlibDef member, IConfuserComponent parentComp) {
			if (member == null) throw new ArgumentNullException("member");
			if (member is ModuleDef) throw new ArgumentException("New ModuleDef cannot be marked.");
			if (IsMarked(context, member)) // avoid double marking
				return;

			marker.MarkMember(member, context);
			if (parentComp != null)
				helperParents[member] = parentComp;
		}

		/// <inheritdoc />
		public bool IsMarked(IConfuserContext context, IDnlibDef def) =>
			ProtectionParameters.HasParameters(context, def);

		/// <inheritdoc />
		public IConfuserComponent GetHelperParent(IDnlibDef def) {
			if (helperParents.TryGetValue(def, out var parent))
				return parent;
			return null;
		}

		public StrongNameKey GetStrongNameKey(IConfuserContext context, ModuleDefMD module) {
			return context.Annotations.Get<StrongNameKey>(module, Marker.SNKey);
		}
	}
}
