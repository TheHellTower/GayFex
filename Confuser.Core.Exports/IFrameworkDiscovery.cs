using System;
using System.Collections.Generic;
using System.Text;

namespace Confuser.Core {
	/// <summary>
	///     Represents a class that is able to discover framework versions.
	/// </summary>
	/// <remarks>
	///     The components are discovered using MEF (Managed Extensibility Framework)
	/// </remarks>
	/// <seealso cref="!:https://docs.microsoft.com/dotnet/framework/mef/"/>
	public interface IFrameworkDiscovery {
		IEnumerable<IInstalledFramework> GetInstalledFrameworks();
	}
}
