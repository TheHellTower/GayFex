using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Confuser.Core {
	/// <summary>
	///     Provides a set of utility methods
	/// </summary>
	public static class Utils {
		static readonly char[] hexCharset = "0123456789abcdef".ToCharArray();

		/// <summary>
		///     Gets the value associated with the specified key, or default value if the key does not exists.
		/// </summary>
		/// <typeparam name="TKey">The type of the key.</typeparam>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <param name="dictionary">The dictionary.</param>
		/// <param name="key">The key of the value to get.</param>
		/// <param name="defValue">The default value.</param>
		/// <returns>The value associated with the specified key, or the default value if the key does not exists</returns>
		public static TValue GetValueOrDefault<TKey, TValue>(
			this Dictionary<TKey, TValue> dictionary,
			TKey key,
			TValue defValue = default(TValue)) {
			TValue ret;
			if (dictionary.TryGetValue(key, out ret))
				return ret;
			return defValue;
		}

		/// <summary>
		///     Gets the value associated with the specified key, or default value if the key does not exists.
		/// </summary>
		/// <typeparam name="TKey">The type of the key.</typeparam>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <param name="dictionary">The dictionary.</param>
		/// <param name="key">The key of the value to get.</param>
		/// <param name="defValueFactory">The default value factory function.</param>
		/// <returns>The value associated with the specified key, or the default value if the key does not exists</returns>
		public static TValue GetValueOrDefaultLazy<TKey, TValue>(
			this Dictionary<TKey, TValue> dictionary,
			TKey key,
			Func<TKey, TValue> defValueFactory) {
			TValue ret;
			if (dictionary.TryGetValue(key, out ret))
				return ret;
			return defValueFactory(key);
		}

		/// <summary>
		///     Adds the specified key and value to the multi dictionary.
		/// </summary>
		/// <typeparam name="TKey">The type of key.</typeparam>
		/// <typeparam name="TValue">The type of value.</typeparam>
		/// <param name="self">The dictionary to add to.</param>
		/// <param name="key">The key of the element to add.</param>
		/// <param name="value">The value of the element to add.</param>
		/// <exception cref="System.ArgumentNullException">key is <c>null</c>.</exception>
		public static void AddListEntry<TKey, TValue>(this IDictionary<TKey, List<TValue>> self, TKey key,
			TValue value) {
			if (key == null)
				throw new ArgumentNullException("key");
			List<TValue> list;
			if (!self.TryGetValue(key, out list))
				list = self[key] = new List<TValue>();
			list.Add(value);
		}

		/// <summary>
		///     Obtains the relative path from the specified base path.
		/// </summary>
		/// <param name="filespec">The file path.</param>
		/// <param name="folder">The base path.</param>
		/// <returns>The path of <paramref name="filespec" /> relative to <paramref name="folder" />.</returns>
		public static string GetRelativePath(string filespec, string folder) {
			//http://stackoverflow.com/a/703292/462805

			var pathUri = new Uri(filespec);
			// Folders must end in a slash
			if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString())) {
				folder += Path.DirectorySeparatorChar;
			}

			var folderUri = new Uri(folder);
			return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString()
				.Replace('/', Path.DirectorySeparatorChar));
		}

		/// <summary>
		///     If the input string is empty, return null; otherwise, return the original input string.
		/// </summary>
		/// <param name="val">The input string.</param>
		/// <returns><c>null</c> if the input string is empty; otherwise, the original input string.</returns>
		public static string NullIfEmpty(this string val) {
			if (string.IsNullOrEmpty(val))
				return null;
			return val;
		}

		/// <summary>
		///     Removes all elements that match the conditions defined by the specified predicate from a the list.
		/// </summary>
		/// <typeparam name="T">The type of the elements of <paramref name="self" />.</typeparam>
		/// <param name="self">The list to remove from.</param>
		/// <param name="match">The predicate that defines the conditions of the elements to remove.</param>
		/// <returns><paramref name="self" /> for method chaining.</returns>
		public static IList<T> RemoveWhere<T>(this IList<T> self, Predicate<T> match) {
			for (int i = self.Count - 1; i >= 0; i--) {
				if (match(self[i]))
					self.RemoveAt(i);
			}

			return self;
		}
	}
}
