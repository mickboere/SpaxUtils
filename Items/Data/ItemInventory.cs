using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// Collection of <see cref="RuntimeItemData"/>s.
	/// </summary>
	public class ItemInventory : IDisposable
	{
		public event Action ClearedInventoryEvent;
		public event Action<IReadOnlyDictionary<string, RuntimeItemData>> ReloadedInventoryEvent;
		public event Action<RuntimeItemData> AddedItemEvent;
		public event Action<RuntimeItemData> RemovedItemEvent;
		public event Action<RuntimeDataEntry> DataEntryUpdatedEvent;

		/// <summary>
		/// The inventory data collection. (<see cref="RuntimeDataCollection.ID"/>, <see cref="RuntimeItemData"/>)
		/// </summary>
		public IReadOnlyDictionary<string, RuntimeItemData> Inventory => inventory;

		private IDependencyManager dependencies;
		private IItemDatabase itemDatabase;
		private RuntimeDataCollection inventoryData;
		private Dictionary<string, RuntimeItemData> inventory;

		public ItemInventory(IDependencyManager dependencies, IItemDatabase itemDatabase, RuntimeDataCollection inventoryData)
		{
			this.dependencies = dependencies;
			this.itemDatabase = itemDatabase;

			inventory = new Dictionary<string, RuntimeItemData>();

			this.inventoryData = inventoryData;
			this.inventoryData.DataUpdatedEvent += OnDataEntryUpdatedEvent;

			ReloadInventory();
		}

		public void Dispose()
		{
			inventoryData.DataUpdatedEvent -= OnDataEntryUpdatedEvent;
			ClearInventory();
		}

		/// <summary>
		/// Disposes of all stored <see cref="RuntimeItemData"/>s.
		/// </summary>
		public void ClearInventory()
		{
			foreach (KeyValuePair<string, RuntimeItemData> entry in inventory)
			{
				entry.Value.Dispose();
			}
			inventory.Clear();
		}

		/// <summary>
		/// Disposes of everything in the inventory and reloads the raw data.
		/// </summary>
		public void ReloadInventory()
		{
			ClearInventory();

			List<RuntimeDataCollection> data = inventoryData.GetEntries<RuntimeDataCollection>();
			foreach (RuntimeDataCollection runtimeData in data)
			{
				AddItem(runtimeData);
			}

			ReloadedInventoryEvent?.Invoke(inventory);
		}

		#region Adding

		/// <summary>
		/// Creates a <see cref="RuntimeItemData"/> and adds it to the inventory.
		/// </summary>
		/// <param name="runtimeData">The runtime data belonging to the item.</param>
		/// <param name="itemData">The item data asset.</param>
		public void AddItem(RuntimeDataCollection runtimeData, IItemData itemData)
		{
			RuntimeDataCollection data = new RuntimeDataCollection(runtimeData.UID, new List<RuntimeDataEntry>(), inventoryData).Append(runtimeData, itemData.Data);

			if (inventoryData.GetEntry(data.UID) != null)
			{
				// Create dependency manager for the item data and bind it.
				DependencyManager dependencyManager = new DependencyManager(dependencies, $"[ITEM]{itemData.Name}");
				RuntimeItemData runtimeItemData = new RuntimeItemData(data, itemData, dependencyManager);
				dependencyManager.Bind(runtimeItemData);

				RuntimeItemData itemStack = inventory.Values.FirstOrDefault((v) => !v.Unique && v.ItemID == runtimeItemData.ItemID);
				if (!runtimeItemData.Unique && itemStack != null)
				{
					// Non-unique (stacking) item of which we already have an instance, increase the quantity of the existing one.
					itemStack.RuntimeData.Set(ItemDataIdentifierConstants.QUANTITY, itemStack.Quantity + runtimeItemData.Quantity);
					data.Dispose();
					runtimeItemData.Dispose();
				}
				else
				{
					// New item, add it to the inventory and invoke necessary methods/events.
					inventoryData.TryAdd(data);
					inventory.Add(runtimeItemData.RuntimeData.UID, runtimeItemData);
					runtimeItemData.ExecuteBehaviour();
					AddedItemEvent?.Invoke(runtimeItemData);
				}
			}
			else
			{
				SpaxDebug.Error("Inventory already contains data with same runtime ID; ", data.UID);
				data.Dispose();
			}
		}

		/// <summary>
		/// Retrieves the item data belonging to <paramref name="runtimeData"/>, then creates a <see cref="RuntimeItemData"/> and adds it to the inventory.
		/// </summary>
		/// <param name="runtimeData">The runtime data belonging to the item.</param>
		/// <param name="itemID">The ID of the item type (optional when runtime data contains the ID.)</param>
		public void AddItem(RuntimeDataCollection runtimeData, string itemID = null)
		{
			// Get IItemData from database.
			IItemData itemData = itemDatabase.GetItem(itemID ?? runtimeData.Get<string>(ItemDataIdentifierConstants.ITEM_ID));
			AddItem(runtimeData, itemData);
		}

		/// <summary>
		/// Adds a new item of type ID <paramref name="itemID"/> to the inventory.
		/// </summary>
		/// <param name="itemID">The ID of the type of item to add an instance of.</param>
		public void AddNewItem(string itemID)
		{
			RuntimeDataCollection runtimeData = RuntimeDataCollection.New();
			AddItem(runtimeData, itemID);
		}

		/// <summary>
		/// Adds a new item of type <paramref name="item"/> to the inventory.
		/// </summary>
		/// <param name="item">The <see cref="IItemData"/> to add to this inventory.</param>
		public void AddNewItem(IItemData item)
		{
			RuntimeDataCollection runtimeData = RuntimeDataCollection.New();
			AddItem(runtimeData, item);
		}

		#endregion

		#region Removing

		/// <summary>
		/// Removes runtime item with ID <paramref name="runtimeItemID"/> from the inventory.
		/// </summary>
		/// <param name="runtimeItemID">The ID of the runtime item to remove.</param>
		public void RemoveItem(string runtimeItemID)
		{
			RuntimeItemData data = Get(runtimeItemID);

			if (data != null)
			{
				inventory.Remove(runtimeItemID);
				RemovedItemEvent?.Invoke(data);
				data.Dispose();
			}
		}

		#endregion

		#region Getting

		/// <summary>
		/// Returns runtime item with ID <paramref name="runtimeItemID"/>.
		/// </summary>
		/// <param name="runtimeItemID">The ID of the runtime item to return.</param>
		/// <returns>Runtime item with ID <paramref name="runtimeItemID"/>.</returns>
		public RuntimeItemData Get(string runtimeItemID)
		{
			return inventory[runtimeItemID];
		}

		/// <summary>
		/// Function that utilizes a <paramref name="predicate"/> <see cref="Func{T, TResult}"/> to filter the items.
		/// </summary>
		public List<RuntimeItemData> Get(Func<RuntimeItemData, bool> predicate)
		{
			return inventory.Values.Where((i) => predicate(i)).ToList();
		}

		#endregion

		private void OnDataEntryUpdatedEvent(RuntimeDataEntry dataEntry)
		{
			DataEntryUpdatedEvent?.Invoke(dataEntry);
		}
	}
}
