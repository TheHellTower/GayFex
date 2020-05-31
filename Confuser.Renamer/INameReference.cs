using Confuser.Core;

namespace Confuser.Renamer {
	public interface INameReference {
		/// <summary>
		///		Check if the element with this reference attached should be
		///		renamed at all.
		/// </summary>
		bool ShouldCancelRename { get; }

		/// <summary>
		///		Update the name reference.
		/// </summary>
		/// <param name="context">The confuser context</param>
		/// <param name="service">The name service</param>
		/// <returns>
		///		<see langword="true" /> in case the name was updated;
		///		otherwise <see langword="false" />.
		///	</returns>
		///	<exception cref="ArgumentNullException">
		///		<paramref name="context" /> is <see langword="null" />
		///		<br />- or -<br />
		///		<paramref name="service" /> is <see langword="null" />
		/// </exception>
		bool UpdateNameReference(ConfuserContext context, INameService service);
		
		/// <summary>
		///		Get a description of this reference, containing the original
		///		names of the referenced objects.
		/// </summary>
		/// <param name="nameService">
		///		The name service used to get the original names;
		///		or <see langword="null"/>
		///	</param>
		/// <returns>Description of this reference.</returns>
		string ToString(INameService nameService);
	}

	public interface INameReference<out T> : INameReference { }
}
