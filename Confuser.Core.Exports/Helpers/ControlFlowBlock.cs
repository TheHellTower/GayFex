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
		public Instruction Footer { get; }

		/// <summary>
		///     The header instruction
		/// </summary>
		public Instruction Header { get; }

		/// <summary>
		///     The identifier of this block
		/// </summary>
		public int Id { get; }

		/// <summary>
		///     The type of this block
		/// </summary>
		public ControlFlowBlockType Type { get; }

		/// <summary>
		///     Gets the source blocks of this control flow block.
		/// </summary>
		/// <value>The source blocks.</value>
		public IList<ControlFlowBlock> Sources { get; }

		/// <summary>
		///     Gets the target blocks of this control flow block.
		/// </summary>
		/// <value>The target blocks.</value>
		public IList<ControlFlowBlock> Targets { get; }

		internal ControlFlowBlock(int id, ControlFlowBlockType type, Instruction header, Instruction footer) {
			Id = id;
			Type = type;
			Header = header;
			Footer = footer;

			Sources = new List<ControlFlowBlock>();
			Targets = new List<ControlFlowBlock>();
		}

		/// <summary>
		///     Returns a <see cref="System.String" /> that represents this block.
		/// </summary>
		/// <returns>A <see cref="System.String" /> that represents this block.</returns>
		public override string ToString() =>
			$"Block {Id} => {Type} {string.Join(", ", Targets.Select(block => block.Id))}";
	}
}
