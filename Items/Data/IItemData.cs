using UnityEngine;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for unique item data.
	/// </summary>
	public interface IItemData : IIdentifiable
	{
		/// <summary>
		/// The name of this item.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// The text describing this item.
		/// </summary>
		string Description { get; }

		/// <summary>
		/// The type/category of item this is.
		/// </summary>
		string Category { get; }

		/// <summary>
		/// Whether this is a unique item or a quantifiable item.
		/// Unique items cannot be stacked and will always count as a new data entry when added to an inventory.
		/// </summary>
		bool Unique { get; }

		/// <summary>
		/// The icon of this item.
		/// </summary>
		Sprite Icon { get; }

		/// <summary>
		/// The prefab to use when spawning this item in the world.
		/// </summary>
		GameObject WorldItemPrefab { get; }

		/// <summary>
		/// The behaviour of this item when it's in an inventory.
		/// </summary>
		IReadOnlyList<BehaviourAsset> InventoryBehaviour { get; }

		/// <summary>
		/// The (float) stats of this item.
		/// </summary>
		IReadOnlyList<LabeledFloatData> FloatStats { get; }
	}
}
