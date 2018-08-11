using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Confuser.Core {
	/// <summary>
	///     Base class of protection phases.
	/// </summary>
	public interface IProtectionPhase {
		/// <summary>
		///     Gets the parent component.
		/// </summary>
		/// <value>The parent component.</value>
		IConfuserComponent Parent { get; }

		/// <summary>
		///     Gets the targets of protection.
		/// </summary>
		/// <returns>The protection targets.</returns>
		ProtectionTargets Targets { get; }

		/// <summary>
		///     Gets the name of the phase.
		/// </summary>
		/// <value>The name of phase.</value>
		string Name { get; }

		/// <summary>
		///     Gets a value indicating whether this phase process all targets, not just the targets that requires the component.
		/// </summary>
		/// <value>
		///     <see langword="true" /> if this phase process all targets; otherwise, <see langword="false" />.
		/// </value>
		bool ProcessAll { get; }

		/// <summary>
		///     Executes the protection phase.
		/// </summary>
		/// <param name="context">The working context.</param>
		/// <param name="parameters">The parameters of protection.</param>
		/// <param name="token">The token used to check if the current operation needs to be canceled.</param>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="context"/> is <see langword="null" />
		///     <br />- or -<br />
		///     <paramref name="parameters"/> is <see langword="null" />
		/// </exception>
		/// <exception cref="OperationCanceledException"><paramref name="token"/> is set to canceled</exception>
		void Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token);
	}
}
