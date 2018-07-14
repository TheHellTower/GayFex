using System;

namespace Confuser.Core {
	/// <summary>
	///     Provides methods to annotate objects.
	/// </summary>
	public interface IAnnotations {
		/// <summary>
		///     Retrieves the annotation on the specified object associated with the specified key.
		/// </summary>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <param name="obj">The object.</param>
		/// <param name="key">The key of annotation.</param>
		/// <param name="defValue">The default value if the specified annotation does not exists on the object.</param>
		/// <returns>The value of annotation, or default value if the annotation does not exist.</returns>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="obj" /> or <paramref name="key" /> is <see langword="null" />.
		/// </exception>
		TValue Get<TValue>(object obj, object key, TValue defValue = default(TValue));

		/// <summary>
		///     Retrieves the annotation on the specified object associated with the specified key.
		/// </summary>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <param name="obj">The object.</param>
		/// <param name="key">The key of annotation.</param>
		/// <param name="defValueFactory">The default value factory function.</param>
		/// <returns>The value of annotation, or default value if the annotation does not exist.</returns>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="obj" /> or <paramref name="key" /> is <see langword="null" />.
		/// </exception>
		TValue GetLazy<TValue>(object obj, object key, Func<object, TValue> defValueFactory);

		/// <summary>
		///     Retrieves or create the annotation on the specified object associated with the specified key.
		/// </summary>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <param name="obj">The object.</param>
		/// <param name="key">The key of annotation.</param>
		/// <param name="factory">The factory function to create the annotation value when the annotation does not exist.</param>
		/// <returns>The value of annotation, or the newly created value.</returns>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="obj" /> or <paramref name="key" /> is <see langword="null" />.
		/// </exception>
		TValue GetOrCreate<TValue>(object obj, object key, Func<object, TValue> factory);

		/// <summary>
		///     Sets an annotation on the specified object.
		/// </summary>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <param name="obj">The object.</param>
		/// <param name="key">The key of annotation.</param>
		/// <param name="value">The value of annotation.</param>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="obj" /> or <paramref name="key" /> is <see langword="null" />.
		/// </exception>
		void Set<TValue>(object obj, object key, TValue value);
	}
}
