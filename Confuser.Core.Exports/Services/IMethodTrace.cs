using dnlib.DotNet.Emit;

namespace Confuser.Core.Services {
	public interface IMethodTrace {
		/// <summary>
		///     Traces the arguments of the specified call instruction.
		/// </summary>
		/// <param name="instr">The call instruction.</param>
		/// <returns>The indexes of the begin instruction of arguments.</returns>
		/// <exception cref="System.ArgumentException">The specified call instruction is invalid.</exception>
		/// <exception cref="InvalidMethodException">The method body is invalid.</exception>
		int[] TraceArguments(Instruction instr);
	}
}
