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
		/// The identification data of this item.
		/// </summary>
		IIdentification Identification { get; }

		/// <summary>
		/// The text describing this item.
		/// </summary>
		string Description { get; }

		/// <summary>
		/// Whether this is a unique item or a quantifiable item.
		/// Unique items cannot be stacked and will always count as a new data entry when added to an inventory.
		/// </summary>
		bool Unique { get; }

		/// <summary>
		/// The base value of this item.
		/// </summary>
		int Value { get; }

		/// <summary>
		/// The icon of this item.
		/// </summary>
		Sprite Icon { get; }

		/// <summary>
		/// The behaviour of this item when it's in an inventory.
		/// </summary>
		IReadOnlyList<BehaviourAsset> InventoryBehaviour { get; }

		/// <summary>
		/// The base item data.
		/// </summary>
		RuntimeDataCollection Data { get; }
	}
}
