using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Confuser.Core {

	/// <summary>
	///     Base class of Confuser packers.
	/// </summary>
	public interface IPacker : IConfuserComponent {
		/// <summary>
		///     Executes the packer.
		/// </summary>
		/// <param name="context">The working context.</param>
		/// <param name="parameters">The parameters of packer.</param>
		/// <param name="token">The token used to cancel the packing operation.</param>
		void Pack(IConfuserContext context, IProtectionParameters parameters, CancellationToken token);
	}
}
