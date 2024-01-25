namespace SpaxUtils
{
	/// <summary>
	/// Base interface for acts, <see cref="Act{T}"/>.
	/// </summary>
	public interface IAct
	{
		/// <summary>
		/// Title of the act, used to identify whether this act can be performed by an <see cref="IPerformer"/> or not.
		/// </summary>
		string Title { get; }

		/// <summary>
		/// Whether this act can be interupted by other acts.
		/// </summary>
		bool Interuptable { get; }

		/// <summary>
		/// Whether this act should attempt to interupt other acts.
		/// </summary>
		bool Interuptor { get; }

		/// <summary>
		/// Action retry-window when initial production attempt fails.
		/// </summary>
		float Buffer { get; }
	}
}
