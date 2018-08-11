using System.Collections.Generic;

namespace Confuser.Core {
	public static class LoggerUtilities {
		/// <summary>
		///     Returns a <see cref="IEnumerable{T}" /> that log the progress of iterating the specified list.
		/// </summary>
		/// <typeparam name="T">The type of list element</typeparam>
		/// <param name="enumerable">The list.</param>
		/// <param name="logger">The logger.</param>
		/// <returns>A wrapper of the list.</returns>
		public static IEnumerable<T> WithProgress<T>(this IEnumerable<T> enumerable, ILogger logger) {
			var list = new List<T>(enumerable);
			int i;
			for (i = 0; i < list.Count; i++) {
				logger.Progress(i, list.Count);
				yield return list[i];
			}
			logger.Progress(i, list.Count);
			logger.EndProgress();
		}
	}
}
