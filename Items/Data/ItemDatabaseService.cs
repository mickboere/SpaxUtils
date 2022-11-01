using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Database service that (should) contain references to all <see cref="IItemData"/>s.
	/// Will load all resources of type <see cref="ItemDataAsset"/> from the "Items" resources folder.
	/// </summary>
	public class ItemDatabaseService : IService, IItemDatabase, IDisposable
	{
		public const string UID_INCREMENTAL = "Incremental";
		public const string UID_GUID = "GUID";

		public Dictionary<string, IItemData> items;

		public ItemDatabaseService()
		{
			LoadItems();
		}

		private void LoadItems()
		{
			items?.Clear();
			items = new Dictionary<string, IItemData>();
			// TODO: Make this service part of SpaxUtils and solve for LoadAll<IItemData>
			IItemData[] resources = Resources.LoadAll<ItemDataAsset>("Items");
			foreach (IItemData resource in resources)
			{
				AddItem(resource);
			}
		}

		public void AddItem(IItemData itemData)
		{
			if (items.ContainsKey(itemData.UID))
			{
				Debug.LogError($"ItemData with ID '{itemData.UID}' already exists within the database!", itemData is UnityEngine.Object o ? o : null);
				return;
			}

			items.Add(itemData.UID, itemData);
			items.Add(itemData.Name, itemData);
		}

		public IItemData GetItem(string id)
		{
			if (items.ContainsKey(id))
			{
				return items[id];
			}

			return null;
		}

		public bool HasItem(string id)
		{
			return items.ContainsKey(id);
		}

		/// <summary>
		/// Returns a new unique item ID, using method <paramref name="method"/>.
		/// If no matching method can be found, reverts to <see cref="Guid.NewGuid()"/>.
		/// </summary>
		/// <param name="method">The method to utilize when generating this unique ID.</param>
		/// <returns>A new unique item ID.</returns>
		public string GetUniqueID(string method)
		{
			switch (method)
			{
				case UID_INCREMENTAL:
					return UniqueIDMethodIncremental();
				case UID_GUID:
				default:
					return Guid.NewGuid().ToString();
			}
		}

		public void Dispose()
		{
			items.Clear();
		}

		private string UniqueIDMethodIncremental()
		{
			// Store all ID's that parse to int.
			List<int> indices = new List<int>();
			foreach (string id in items.Keys)
			{
				if (int.TryParse(id, out int i))
				{
					indices.Add(i);
				}
			}

			// Keep incrementing until no matching ID is found.
			int index = 0;
			while (indices.Contains(index))
			{
				index++;
			}

			return index.ToString();
		}
	}
}
