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
		/// Returns whether there is any runtime data stored for <paramref name="identifier"/>.
		/// If data doesn't exist yet, a new entry will attempt to be added from <see cref="IItemData.FloatStats"/>.
		/// </summary>
		/// <param name="identifier">The identifier of the stat data to retrieve the value from.</param>
		/// <returns>TRUE if success, FALSE is not. OUT: Value for stat data with id <paramref name="identifier"/>.</returns>
		bool TryGetData(string identifier, out RuntimeDataEntry data);

		/// <summary>
		/// Wraps around <see cref="TryGetData(string, out RuntimeDataEntry)"/>, returning the data value as a float.
		/// </summary>
		bool TryGetStat(string identifier, out float value);
	}
}
