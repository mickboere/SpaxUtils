using SpaxUtils;
using SpaxUtils.StateMachines;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// Item data object instanced when an item is in an inventory.
	/// </summary>
	public class RuntimeItemData : IRuntimeDataContainer, IDisposable
	{
		/// <summary>
		/// Invoked when this runtime item data is being disposed of.
		/// </summary>
		public event Action<RuntimeItemData> DisposeEvent;

		#region Properties

		/// <summary>
		/// The asset data for this item.
		/// </summary>
		public IItemData ItemData { get; private set; }

		/// <summary>
		/// The runtime data for this item.
		/// </summary>
		public RuntimeDataCollection RuntimeData { get; private set; }

		/// <summary>
		/// The dependency manager belonging to this runtime item data.
		/// </summary>
		public IDependencyManager DependencyManager { get; private set; }

		/// <summary>
		/// The ID of the runtime data.
		/// </summary>
		public string RuntimeID => RuntimeData.ID;

		/// <summary>
		/// The ID of the item data.
		/// </summary>
		public string ItemID => ItemData != null ? ItemData.ID : "";

		/// <summary>
		/// All inventory behaviours running for this item.
		/// </summary>
		public List<BehaviourAsset> Behaviours { get; private set; } = new List<BehaviourAsset>();

		#endregion Properties

		#region Standard Data Wrappers

		/// <summary>
		/// The name of this item.
		/// </summary>
		public string Name => RuntimeData.GetValue<string>(ItemDataIdentifiers.NAME) ?? ItemData.Identification.Name;

		/// <summary>
		/// Whether this item is unique or otherwise stackable.
		/// </summary>
		public bool Unique => RuntimeData.TryGetValue(ItemDataIdentifiers.UNIQUE, out bool unique) ? unique : ItemData.Unique;

		/// <summary>
		/// The quantity of this item.
		/// </summary>
		public int Quantity => Unique ? 1 : RuntimeData.TryGetValue(ItemDataIdentifiers.QUANTITY, out int quantity) ? quantity : 1;

		/// <summary>
		/// The total value of this item multiplied by its quantity.
		/// </summary>
		public float Value => (RuntimeData.TryGetValue(ItemDataIdentifiers.VALUE, out float value) ? value : ItemData.Value) * Quantity;

		#endregion Standard Data Wrappers

		private bool disposing;

		public RuntimeItemData(IItemData itemData, RuntimeDataCollection runtimeData,
			IDependencyManager dependencyManager = null)
		{
			ItemData = itemData;
			RuntimeData = runtimeData;
			DependencyManager = dependencyManager;

			// Mandatory data used when loading item data.
			RuntimeData.SetValue(ItemDataIdentifiers.ITEM_ID, ItemID);

			// Append base data without overriding.
			RuntimeData.Append(itemData.Data, false);
		}

		public void InitializeBehaviour()
		{
			if (DependencyManager == null)
			{
				SpaxDebug.Error("Cannot initialize runtime item behaviours because there is no dependency manager.");
				return;
			}

			foreach (BehaviourAsset behaviour in ItemData.InventoryBehaviour)
			{
				BehaviourAsset behaviourInstance = behaviour.CreateInstance();
				Behaviours.Add(behaviourInstance);
				DependencyManager.Inject(behaviourInstance);
				behaviourInstance.Start();
			}
		}

		public bool TryGetBehaviour<T>(out T behaviour) where T : class
		{
			foreach (BehaviourAsset b in Behaviours)
			{
				if (b is T cast)
				{
					behaviour = cast;
					return true;
				}
			}

			behaviour = null;
			return false;
		}

		public void Dispose()
		{
			if (disposing)
			{
				return;
			}

			disposing = true;
			foreach (BehaviourAsset behaviour in Behaviours)
			{
				behaviour.Destroy();
			}
			DisposeEvent?.Invoke(this);
		}

		public override string ToString()
		{
			return $"RuntimeItemData\n{{\n{ItemData}\n{RuntimeData}\n}}";
		}
	}
}
