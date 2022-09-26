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
	}
}
