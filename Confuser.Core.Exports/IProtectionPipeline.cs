namespace Confuser.Core {
	public interface IProtectionPipeline {
		/// <summary>
		///     Inserts the phase into pre-processing pipeline of the specified stage.
		/// </summary>
		/// <param name="stage">The pipeline stage.</param>
		/// <param name="phase">The protection phase.</param>
		void InsertPreStage(PipelineStage stage, IProtectionPhase phase);

		/// <summary>
		///     Inserts the phase into post-processing pipeline of the specified stage.
		/// </summary>
		/// <param name="stage">The pipeline stage.</param>
		/// <param name="phase">The protection phase.</param>
		void InsertPostStage(PipelineStage stage, IProtectionPhase phase);

		/// <summary>
		///     Finds the phase with the specified type in the pipeline.
		/// </summary>
		/// <typeparam name="T">The type of the phase.</typeparam>
		/// <returns>The phase with specified type in the pipeline.</returns>
		T FindPhase<T>() where T : IProtectionPhase;
	}
}
