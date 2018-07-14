using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet.Emit;

namespace Confuser.Core.Helpers {
	/// <summary>
	///     A block in Control Flow Graph (CFG).
	/// </summary>
	public class ControlFlowBlock {
		/// <summary>
		///     The footer instruction
		/// </summary>
		public readonly Instruction Footer;

		/// <summary>
		///     The header instruction
		/// </summary>
		public readonly Instruction Header;

		/// <summary>
		///     The identifier of this block
		/// </summary>
		public readonly int Id;

		/// <summary>
		///     The type of this block
		/// </summary>
		public readonly ControlFlowBlockType Type;

		internal ControlFlowBlock(int id, ControlFlowBlockType type, Instruction header, Instruction footer) {
			Id = id;
			Type = type;
			Header = header;
			Footer = footer;

			Sources = new List<ControlFlowBlock>();
			Targets = new List<ControlFlowBlock>();
		}

		/// <summary>
		///     Gets the source blocks of this control flow block.
		/// </summary>
		/// <value>The source blocks.</value>
		public IList<ControlFlowBlock> Sources { get; private set; }

		/// <summary>
		///     Gets the target blocks of this control flow block.
		/// </summary>
		/// <value>The target blocks.</value>
		public IList<ControlFlowBlock> Targets { get; private set; }

		/// <summary>
		///     Returns a <see cref="System.String" /> that represents this block.
		/// </summary>
		/// <returns>A <see cref="System.String" /> that represents this block.</returns>
		public override string ToString() {
			return string.Format("Block {0} => {1} {2}", Id, Type, string.Join(", ", Targets.Select(block => block.Id.ToString()).ToArray()));
		}
	}
}
