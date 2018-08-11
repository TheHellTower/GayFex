using System.Threading;
using dnlib.DotNet;

namespace Confuser.Core.Services {
	public interface IPackerService {
		/// <summary>
		///     Protects the stub using original project settings replace the current output with the protected stub.
		/// </summary>
		/// <param name="context">The working context.</param>
		/// <param name="fileName">The result file name.</param>
		/// <param name="module">The stub module.</param>
		/// <param name="snKey">The strong name key.</param>
		/// <param name="prot">The packer protection that applies to the stub.</param>
		void ProtectStub(IConfuserContext context, string fileName, byte[] module, StrongNameKey snKey, IProtection prot, CancellationToken token);
	}
}
