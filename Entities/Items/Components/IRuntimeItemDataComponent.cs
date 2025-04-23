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
	}
}
