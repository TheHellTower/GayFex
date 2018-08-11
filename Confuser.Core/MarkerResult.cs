using System.Collections.Generic;
using System.Collections.Immutable;
using dnlib.DotNet;

namespace Confuser.Core {
	/// <summary>
	///     Result of the marker.
	/// </summary>
	public class MarkerResult {
		/// <summary>
		///     Initializes a new instance of the <see cref="MarkerResult" /> class.
		/// </summary>
		/// <param name="modules">The modules.</param>
		/// <param name="packer">The packer.</param>
		/// <param name="extModules">The external modules.</param>
		public MarkerResult(IImmutableList<ModuleDefMD> modules, IPacker packer, IImmutableList<byte[]> extModules) {
			Modules = modules;
			Packer = packer;
			ExternalModules = extModules;
		}

		/// <summary>
		///     Gets a list of modules that is marked.
		/// </summary>
		/// <value>The list of modules.</value>
		public IImmutableList<ModuleDefMD> Modules { get; }

		/// <summary>
		///     Gets a list of external modules.
		/// </summary>
		/// <value>The list of external modules.</value>
		public IImmutableList<byte[]> ExternalModules { get; }

		/// <summary>
		///     Gets the packer if exists.
		/// </summary>
		/// <value>The packer, or null if no packer exists.</value>
		public IPacker Packer { get; }
	}
}
