namespace SpaxUtils
{
	/// <summary>
	/// Interface for classes containing data for a single item.
	/// </summary>
	public interface IRuntimeItemData
	{
		/// <summary>
		/// The asset data for this item.
		/// </summary>
		IItemData ItemData { get; }

		/// <summary>
		/// The runtime data for this item.
		/// </summary>
		RuntimeDataCollection RuntimeData { get; }

		/// <summary>
		/// Returns whether there is a stored value for stat data with id <paramref name="identifier"/>.
		/// <see cref="RuntimeData"/> overrides <see cref="IItemData.Stats"/>.
		/// </summary>
		/// <param name="identifier">The identifier of the stat data to retrieve the value from.</param>
		/// <returns>TRUE if success, FALSE is not. OUT: Value for stat data with id <paramref name="identifier"/>.</returns>
		bool TryGetStat(string identifier, out float value, float defaultIfNull = 0f);

		/// <summary>
		/// Returns value for stat data with id <paramref name="identifier"/>.
		/// <see cref="RuntimeData"/> overrides <see cref="IItemData.Stats"/>.
		/// </summary>
		/// <param name="identifier">The identifier of the stat data to retrieve the value from.</param>
		/// <returns>Value for stat data with id <paramref name="identifier"/>, <paramref name="defaultIfNull"/> if null.</returns>
		float GetStat(string identifier, float defaultIfNull = 0f);
	}
}
