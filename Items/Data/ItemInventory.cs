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
		public event Action<IReadOnlyDictionary<string, RuntimeItemData>> ReloadedInventoryEvent;
		public event Action<RuntimeItemData> AddedItemEvent;
		public event Action<RuntimeItemData> RemovedItemEvent;
		public event Action<RuntimeDataEntry> DataEntryUpdatedEvent;

		/// <summary>
		/// The inventory data collection. (<see cref="RuntimeDataCollection.ID"/>, <see cref="RuntimeItemData"/>)
		/// </summary>
		public IReadOnlyDictionary<string, RuntimeItemData> Entries => entries;

		private IDependencyManager dependencies;
		private IItemDatabase itemDatabase;
		private RuntimeDataCollection inventoryData;
		private Dictionary<string, RuntimeItemData> entries;

		public ItemInventory(IDependencyManager dependencies, IItemDatabase itemDatabase, RuntimeDataCollection inventoryData)
		{
			this.dependencies = dependencies;
			this.itemDatabase = itemDatabase;
			this.inventoryData = inventoryData;

			entries = new Dictionary<string, RuntimeItemData>();

			inventoryData.DataUpdatedEvent += OnDataEntryUpdatedEvent;

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
			foreach (KeyValuePair<string, RuntimeItemData> entry in entries)
			{
				entry.Value.Dispose();
			}
			entries.Clear();
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

			ReloadedInventoryEvent?.Invoke(entries);
		}

		#region Adding

		/// <summary>
		/// Adds a new item of type <paramref name="item"/> to the inventory.
		/// </summary>
		/// <param name="item">The <see cref="IItemData"/> to add to this inventory.</param>
		public RuntimeItemData AddItem(IItemData item)
		{
			RuntimeDataCollection runtimeData = RuntimeDataCollection.New();
			return AddItem(item, runtimeData);
		}

		/// <summary>
		/// Adds a new item of type ID <paramref name="itemID"/> to the inventory.
		/// </summary>
		/// <param name="itemID">The ID of the type of item to add an instance of.</param>
		public RuntimeItemData AddItem(string itemID)
		{
			RuntimeDataCollection runtimeData = RuntimeDataCollection.New();
			return AddItem(runtimeData, itemID);
		}

		/// <summary>
		/// Adds a (new) item using the data provided by <paramref name="runtimeItemData"/>.
		/// </summary>
		/// <param name="runtimeItemData">The item data to add to the inventory.</param>
		public RuntimeItemData AddItem(IRuntimeItemData runtimeItemData)
		{
			return AddItem(runtimeItemData.ItemData, runtimeItemData.RuntimeData);
		}

		/// <summary>
		/// Retrieves the item data belonging to <paramref name="runtimeData"/>, then creates a <see cref="RuntimeItemData"/> and adds it to the inventory.
		/// </summary>
		/// <param name="runtimeData">The runtime data belonging to the item.</param>
		/// <param name="itemID">The ID of the item type (optional when runtime data contains the ID.)</param>
		public RuntimeItemData AddItem(RuntimeDataCollection runtimeData, string itemID = null)
		{
			IItemData itemData = itemDatabase.GetItem(itemID ?? runtimeData.Get<string>(ItemDataIdentifierConstants.ITEM_ID));
			return AddItem(itemData, runtimeData);
		}

		/// <summary>
		/// Creates a <see cref="RuntimeItemData"/> and adds it to the inventory.
		/// </summary>
		/// <param name="runtimeData">The runtime data belonging to the item.</param>
		/// <param name="itemData">The item asset data.</param>
		public RuntimeItemData AddItem(IItemData itemData, RuntimeDataCollection runtimeData)
		{
			runtimeData = runtimeData != null ? runtimeData.Clone() : new RuntimeDataCollection(Guid.NewGuid().ToString());

			if (!entries.ContainsKey(runtimeData.ID))
			{
				// Create dependency manager for the item data and bind it.
				DependencyManager dependencyManager = new DependencyManager(dependencies, $"[ITEM]{itemData.Name}");
				RuntimeItemData runtimeItemData = new RuntimeItemData(itemData, runtimeData, dependencyManager);
				dependencyManager.Bind(runtimeItemData);

				RuntimeItemData itemStack = entries.Values.FirstOrDefault((v) => !v.Unique && v.ItemID == runtimeItemData.ItemID);
				if (!runtimeItemData.Unique && itemStack != null)
				{
					// Non-unique (stacking) item of which we already have an instance, increase the quantity of the existing one.
					itemStack.RuntimeData.Set(ItemDataIdentifierConstants.QUANTITY, itemStack.Quantity + runtimeItemData.Quantity);
					runtimeData.Dispose();
					runtimeItemData.Dispose();
					return itemStack;
				}
				else
				{
					// New item, add it to the inventory and invoke necessary methods/events.
					inventoryData.TryAdd(runtimeData);
					entries.Add(runtimeItemData.RuntimeID, runtimeItemData);
					runtimeItemData.InitializeBehaviour();
					AddedItemEvent?.Invoke(runtimeItemData);
					return runtimeItemData;
				}
			}
			else
			{
				SpaxDebug.Error("Inventory already contains data with same runtime ID; ", runtimeData.ID);
				runtimeData.Dispose();
				return entries[runtimeData.ID];
			}
		}

		#endregion

		#region Removing

		/// <summary>
		/// Removes runtime item with ID <paramref name="runtimeItemID"/> from the inventory.
		/// </summary>
		/// <param name="runtimeItemID">The ID of the runtime item to remove.</param>
		public void RemoveItem(string runtimeItemID)
		{
			RuntimeItemData runtimeData = Get(runtimeItemID);
			RemoveItem(runtimeData);
		}

		/// <summary>
		/// Removes the <paramref name="runtimeData"/> from the inventory.
		/// </summary>
		/// <param name="runtimeData">The item data to remove.</param>
		public void RemoveItem(RuntimeItemData runtimeData)
		{
			if (runtimeData != null)
			{
				entries.Remove(runtimeData.RuntimeID);
				RemovedItemEvent?.Invoke(runtimeData);
				runtimeData.Dispose();
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
			if (entries.ContainsKey(runtimeItemID))
			{
				return entries[runtimeItemID];
			}
			return null;
		}

		/// <summary>
		/// Returns runtime item which matches <paramref name="itemData"/>.
		/// </summary>
		/// <param name="itemData">The itemData to find a runtime match for.</param>
		/// <returns>The <see cref="RuntimeItemData"/> matching <paramref name="itemData"/>, if any.</returns>
		public RuntimeItemData Get(IItemData itemData)
		{
			foreach (RuntimeItemData entry in entries.Values)
			{
				if (entry.ItemData == itemData)
				{
					return entry;
				}
			}
			return null;
		}

		/// <summary>
		/// Get method that utilizes a func to filter the items and return a list.
		/// </summary>
		public List<RuntimeItemData> Get(Func<RuntimeItemData, bool> predicate)
		{
			return entries.Values.Where((i) => predicate(i)).ToList();
		}

		#endregion

		private void OnDataEntryUpdatedEvent(RuntimeDataEntry dataEntry)
		{
			DataEntryUpdatedEvent?.Invoke(dataEntry);
		}
	}
}
