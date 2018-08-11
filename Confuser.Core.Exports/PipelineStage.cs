namespace Confuser.Core {
	/// <summary>
	///     Various stages in <see cref="IProtectionPipeline" />.
	/// </summary>
	public enum PipelineStage {
		/// <summary>
		///     Confuser engine inspects the loaded modules and makes necessary changes.
		///     This stage occurs only once per pipeline run.
		/// </summary>
		Inspection,

		/// <summary>
		///     Confuser engine begins to process a module.
		///     This stage occurs once per module.
		/// </summary>
		BeginModule,

		/// <summary>
		///     Confuser engine processes a module.
		///     This stage occurs once per module.
		/// </summary>
		ProcessModule,

		/// <summary>
		///     Confuser engine optimizes opcodes of the method bodys.
		///     This stage occurs once per module.
		/// </summary>
		OptimizeMethods,

		/// <summary>
		///     Confuser engine finishes processing a module.
		///     This stage occurs once per module.
		/// </summary>
		EndModule,

		/// <summary>
		///     Confuser engine writes the module to byte array.
		///     This stage occurs once per module, after all processing of modules are completed.
		/// </summary>
		WriteModule,

		/// <summary>
		///     Confuser engine generates debug symbols.
		///     This stage occurs only once per pipeline run.
		/// </summary>
		Debug,

		/// <summary>
		///     Confuser engine packs up the output if packer is present.
		///     This stage occurs only once per pipeline run.
		/// </summary>
		Pack,

		/// <summary>
		///     Confuser engine saves the output.
		///     This stage occurs only once per pipeline run.
		/// </summary>
		SaveModules
	}
}
