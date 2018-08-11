using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Confuser.Core {
	/// <summary>
	///     Resolves dependency between protections.
	/// </summary>
	internal sealed class DependencyResolver {
		private readonly List<Lazy<IProtection, IProtectionMetadata>> protections;

		/// <summary>
		///     Initializes a new instance of the <see cref="DependencyResolver" /> class.
		/// </summary>
		/// <param name="protections">The protections for resolution.</param>
		public DependencyResolver(IEnumerable<Lazy<IProtection, IProtectionMetadata>> protections) {
			if (protections == null) throw new ArgumentNullException(nameof(protections));

			this.protections = protections.ToList();
		}

		/// <summary>
		///     Sort the protection according to their dependency.
		/// </summary>
		/// <returns>Sorted protections with respect to dependencies.</returns>
		/// <exception cref="T:CircularDependencyException">
		///     The protections contain circular dependencies.
		/// </exception>
		public IList<IProtection> SortDependency() {
			/* Here we do a topological sort of the protections.
			 * First we construct a dependency graph of the protections.
			 * The edges in the graph is recorded in a list.
			 * Then the graph is sorted starting from the null root node.
			 */

			var edges = new List<DependencyGraphEdge>();
			var roots = new HashSet<IProtection>(protections.Select(lazy => lazy.Value));
			var id2prot = protections.ToDictionary(lazy => lazy.Metadata.Id, lazy => lazy.Value, StringComparer.Ordinal);

			foreach (var prot in protections) {
				Type protType = prot.GetType();

				BeforeProtectionAttribute before = protType
					.GetCustomAttributes(typeof(BeforeProtectionAttribute), false)
					.Cast<BeforeProtectionAttribute>()
					.SingleOrDefault();
				if (before != null) {
					// current -> target
					var targets = before.Ids.Select(id => id2prot[id]);
					foreach (var target in targets) {
						edges.Add(new DependencyGraphEdge(prot.Value, target));
						roots.Remove(target);
					}
				}

				AfterProtectionAttribute after = protType
					.GetCustomAttributes(typeof(AfterProtectionAttribute), false)
					.Cast<AfterProtectionAttribute>()
					.SingleOrDefault();
				if (after != null) {
					// target -> current
					var targets = after.Ids.Select(id => id2prot[id]);
					foreach (var target in targets) {
						edges.Add(new DependencyGraphEdge(target, prot.Value));
						roots.Remove(prot.Value);
					}
				}
			}

			var sorted = SortGraph(roots, edges);
			return sorted.ToList();
		}

		/// <summary>
		///     Topologically sort the dependency graph.
		/// </summary>
		/// <param name="roots">The root protections.</param>
		/// <param name="edges">The dependency graph edges.</param>
		/// <returns>Topological sorted protections.</returns>
		IEnumerable<IProtection> SortGraph(IEnumerable<IProtection> roots, IList<DependencyGraphEdge> edges) {
			var queue = new Queue<IProtection>(roots);
			while (queue.Count > 0) {
				var root = queue.Dequeue(); // Find a node with no incoming edges
				Debug.Assert(!edges.Where(edge => edge.To == root).Any());
				yield return root;

				foreach (DependencyGraphEdge edge in edges.Where(edge => edge.From == root).ToList()) {
					edges.Remove(edge);
					if (!edges.Any(e => e.To == edge.To)) // No more incoming edge to edge.To
						queue.Enqueue(edge.To); // Add new root node
				}
			}
			if (edges.Count != 0)
				throw new CircularDependencyException(edges[0].From, edges[0].To);
		}

		/// <summary>
		///     An edge of dependency graph.
		/// </summary>
		class DependencyGraphEdge {
			/// <summary>
			///     Initializes a new instance of the <see cref="DependencyGraphEdge" /> class.
			/// </summary>
			/// <param name="from">The source protection node.</param>
			/// <param name="to">The destination protection node.</param>
			public DependencyGraphEdge(IProtection from, IProtection to) {
				From = from;
				To = to;
			}

			/// <summary>
			///     The source protection node.
			/// </summary>
			public IProtection From { get; private set; }

			/// <summary>
			///     The destination protection node.
			/// </summary>
			public IProtection To { get; private set; }
		}
	}

	/// <summary>
	///     The exception that is thrown when there exists circular dependency between protections.
	/// </summary>
	internal class CircularDependencyException : Exception {
		/// <summary>
		///     Initializes a new instance of the <see cref="CircularDependencyException" /> class.
		/// </summary>
		/// <param name="a">The first protection.</param>
		/// <param name="b">The second protection.</param>
		internal CircularDependencyException(IProtection a, IProtection b)
			: base(string.Format("The protections '{0}' and '{1}' has a circular dependency between them.", a, b)) {
			Debug.Assert(a != null);
			Debug.Assert(b != null);
			ProtectionA = a;
			ProtectionB = b;
		}

		/// <summary>
		///     First protection that involved in circular dependency.
		/// </summary>
		public IProtection ProtectionA { get; private set; }

		/// <summary>
		///     Second protection that involved in circular dependency.
		/// </summary>
		public IProtection ProtectionB { get; private set; }
	}
}
