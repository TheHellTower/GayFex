using dnlib.DotNet;

namespace Confuser.Core.Services {
	/// <summary>
	///     Provides methods to trace stack of method body.
	/// </summary>
	public interface ITraceService {
		/// <summary>
		///     Trace the stack of the specified method.
		/// </summary>
		/// <param name="method">The method to trace.</param>
		/// <exception cref="dnlib.DotNet.Emit.InvalidMethodException"><paramref name="method" /> has invalid body.</exception>
		/// <exception cref="System.ArgumentNullException"><paramref name="method" /> is <c>null</c>.</exception>
		IMethodTrace Trace(MethodDef method);
	}
}
