using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Lightweight display data for an item notification feed entry.
	/// </summary>
	public struct ItemNotification
	{
		/// <summary>
		/// The display name of the item (or summary text for overflow).
		/// </summary>
		public string Name;

		/// <summary>
		/// The item icon. Null for summary/overflow entries.
		/// </summary>
		public Sprite Icon;

		/// <summary>
		/// The quantity change. Positive for items added, negative for items removed.
		/// </summary>
		public int Quantity;

		public ItemNotification(string name, Sprite icon, int quantity)
		{
			Name = name;
			Icon = icon;
			Quantity = quantity;
		}
	}
}
