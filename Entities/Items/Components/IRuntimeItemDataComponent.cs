namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IEntityComponent"/> refering to an <see cref="RuntimeItemData"/>.
	/// </summary>
	public interface IRuntimeItemDataComponent : IEntityComponent
	{
		/// <summary>
		/// The runtime item data belonging to this entity.
		/// </summary>
		RuntimeItemData RuntimeItemData { get; }

		/// <summary>
		/// Set the <see cref="IRuntimeItemData.ItemData"/>
		/// </summary>
		void SetItemData(IItemData itemData);

		/// <summary>
		/// Set the <see cref="IRuntimeItemData.RuntimeData"/>
		/// </summary>
		void SetRuntimeData(RuntimeDataCollection runtimeData);
	}
}
