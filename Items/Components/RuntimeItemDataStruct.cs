namespace SpaxUtils
{
	/// <summary>
	/// Simple data container for runtime item data, used to pass the data without modifying it.
	/// </summary>
	public struct RuntimeItemDataStruct : IRuntimeItemData
	{
		public IItemData ItemData { get; }

		public RuntimeDataCollection RuntimeData { get; }

		public RuntimeItemDataStruct(IItemData itemData, RuntimeDataCollection runtimeData)
		{
			ItemData = itemData;
			RuntimeData = runtimeData;
		}
	}
}
