using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Confuser.Core.Services;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Confuser.Core {
	/// <summary>
	///     Protection processing pipeline.
	/// </summary>
	public sealed class ProtectionPipeline : IProtectionPipeline {
		readonly Dictionary<PipelineStage, List<IProtectionPhase>> postStage;
		readonly Dictionary<PipelineStage, List<IProtectionPhase>> preStage;

		/// <summary>
		///     Initializes a new instance of the <see cref="ProtectionPipeline" /> class.
		/// </summary>
		public ProtectionPipeline() {
			var stages = (PipelineStage[])Enum.GetValues(typeof(PipelineStage));
			preStage = stages.ToDictionary(stage => stage, stage => new List<IProtectionPhase>());
			postStage = stages.ToDictionary(stage => stage, stage => new List<IProtectionPhase>());
		}

		/// <summary>
		///     Inserts the phase into pre-processing pipeline of the specified stage.
		/// </summary>
		/// <param name="stage">The pipeline stage.</param>
		/// <param name="phase">The protection phase.</param>
		public void InsertPreStage(PipelineStage stage, IProtectionPhase phase) {
			preStage[stage].Add(phase);
		}

		/// <summary>
		///     Inserts the phase into post-processing pipeline of the specified stage.
		/// </summary>
		/// <param name="stage">The pipeline stage.</param>
		/// <param name="phase">The protection phase.</param>
		public void InsertPostStage(PipelineStage stage, IProtectionPhase phase) {
			postStage[stage].Add(phase);
		}

		/// <summary>
		///     Finds the phase with the specified type in the pipeline.
		/// </summary>
		/// <typeparam name="T">The type of the phase.</typeparam>
		/// <returns>The phase with specified type in the pipeline.</returns>
		public T FindPhase<T>() where T : IProtectionPhase {
			foreach (var phases in preStage.Values)
				foreach (var phase in phases) {
					if (phase is T)
						return (T)phase;
				}
			foreach (var phases in postStage.Values)
				foreach (var phase in phases) {
					if (phase is T)
						return (T)phase;
				}
			return default;
		}

		/// <summary>
		///     Execute the specified pipeline stage with pre-processing and post-processing.
		/// </summary>
		/// <param name="stage">The pipeline stage.</param>
		/// <param name="func">The stage function.</param>
		/// <param name="targets">The target list of the stage.</param>
		/// <param name="context">The working context.</param>
		internal void ExecuteStage(PipelineStage stage, Action<ConfuserContext, CancellationToken> func, Func<IList<IDnlibDef>> targets, ConfuserContext context, CancellationToken token) {
			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("Pipeline");

			foreach (var pre in preStage[stage]) {
				token.ThrowIfCancellationRequested();
				logger.LogDebug("Executing '{0}' phase...", pre.Name);
				pre.Execute(context, new ProtectionParameters(pre.Parent, Filter(context, targets(), pre)), token);
			}
			token.ThrowIfCancellationRequested();
			func(context, token);
			token.ThrowIfCancellationRequested();
			foreach (var post in postStage[stage]) {
				logger.LogDebug("Executing '{0}' phase...", post.Name);
				post.Execute(context, new ProtectionParameters(post.Parent, Filter(context, targets(), post)), token);
				token.ThrowIfCancellationRequested();
			}
		}

		/// <summary>
		///     Returns only the targets with the specified type and used by specified component.
		/// </summary>
		/// <param name="context">The working context.</param>
		/// <param name="targets">List of targets.</param>
		/// <param name="phase">The component phase.</param>
		/// <returns>Filtered targets.</returns>
		static IImmutableList<IDnlibDef> Filter(ConfuserContext context, IList<IDnlibDef> targets, IProtectionPhase phase) {
			ProtectionTargets targetType = phase.Targets;

			IEnumerable<IDnlibDef> filter = targets;
			if ((targetType & ProtectionTargets.Modules) == 0)
				filter = filter.Where(def => !(def is ModuleDef));
			if ((targetType & ProtectionTargets.Types) == 0)
				filter = filter.Where(def => !(def is TypeDef));
			if ((targetType & ProtectionTargets.Methods) == 0)
				filter = filter.Where(def => !(def is MethodDef));
			if ((targetType & ProtectionTargets.Fields) == 0)
				filter = filter.Where(def => !(def is FieldDef));
			if ((targetType & ProtectionTargets.Properties) == 0)
				filter = filter.Where(def => !(def is PropertyDef));
			if ((targetType & ProtectionTargets.Events) == 0)
				filter = filter.Where(def => !(def is EventDef));

			if (phase.ProcessAll)
				return filter.ToImmutableArray();

			return filter.Where(def => {
				ProtectionSettings parameters = ProtectionParameters.GetParameters(context, def);
				Debug.Assert(parameters != null);
				if (parameters == null) {
					var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger("core");
					logger.LogCritical("'{0}' not marked for obfuscation, possibly a bug.", def);
					throw new ConfuserException(null);
				}
				return parameters.ContainsKey(phase.Parent);
			}).ToImmutableArray();
		}
	}
}
