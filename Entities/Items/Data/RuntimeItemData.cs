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
		public string ItemID => ItemData != null ? ItemData.ID : "NULL!";

		/// <summary>
		/// All inventory behaviours running for this item.
		/// </summary>
		public List<IBehaviour> Behaviours { get; private set; } = new List<IBehaviour>();

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
		/// The rarity of this item.
		/// </summary>
		public int Rarity => GetRarity();

		/// <summary>
		/// The rank of this item.
		/// </summary>
		public float Rank => RuntimeData.TryGetValue(ItemDataIdentifiers.RANK, out float rank) ? rank : ItemData.Rank;

		/// <summary>
		/// The quality of this item.
		/// </summary>
		public float Quality => RuntimeData.TryGetValue(ItemDataIdentifiers.QUALITY, out float quality) ? quality : ItemData.Quality;

		/// <summary>
		/// The total value of this item multiplied by its quantity.
		/// </summary>
		public int Value => GetValue() * Quantity;

		#endregion Standard Data Wrappers

		private bool disposing;

		public RuntimeItemData(IItemData itemData, RuntimeDataCollection runtimeData,
			IDependencyManager dependencyManager = null)
		{
			ItemData = itemData;
			RuntimeData = runtimeData;
			DependencyManager = dependencyManager;

			// Mandatory data used when loading item data.
			RuntimeData.SetValue(ItemDataIdentifiers.ITEM_ID, ItemID, true, true);

			// Append base data without overriding.
			RuntimeData.AppendCollection(itemData.Data, false);
		}

		public void InitializeBehaviour()
		{
			if (DependencyManager == null)
			{
				SpaxDebug.Error("Cannot initialize runtime item behaviours because there is no dependency manager.");
				return;
			}

			if (ItemData is IEquipmentData eq)
			{
				Add(new EquipmentInventoryBehaviour());
			}

			foreach (BehaviourAsset behaviour in ItemData.InventoryBehaviour)
			{
				BehaviourAsset behaviourInstance = behaviour.CreateInstance();
				Add(behaviourInstance);
			}

			void Add(IBehaviour behaviour)
			{
				Behaviours.Add(behaviour);
				DependencyManager.Inject(behaviour);
				behaviour.Start();
			}
		}

		public bool TryGetBehaviour<T>(out T behaviour) where T : class
		{
			foreach (IBehaviour b in Behaviours)
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
			foreach (IBehaviour behaviour in Behaviours)
			{
				behaviour.Dispose();
			}
			DisposeEvent?.Invoke(this);
		}

		public override string ToString()
		{
			return $"RuntimeItemData\n{{\n{ItemData}\n{RuntimeData}\n}}";
		}

		#region Getters

		public int GetRarity()
		{
			if (RuntimeData.TryGetValue(ItemDataIdentifiers.RARITY, out int rarity))
			{
				return rarity;
			}
			else if (ItemData.Rarity == ItemRarity.Undefined)
			{
				return (int)SpaxFormulas.GetRarityFromQuality(Quality);
			}
			else
			{
				return (int)ItemData.Rarity;
			}
		}

		public int GetValue()
		{
			if (RuntimeData.TryGetValue(ItemDataIdentifiers.VALUE, out int value))
			{
				return value;
			}
			else if (ItemData.Value < 0)
			{
				// Calculate value as budget.
				return CalculateBudget().RoundToInt();
			}
			else
			{
				return ItemData.Value;
			}
		}

		/// <summary>
		/// Calculates the total Point budget for this item based on its Rank and Quality.
		/// </summary>
		/// <returns></returns>
		public float CalculateBudget()
		{
			return SpaxFormulas.PointsFromRank(Rank) * Quality;
		}

		#endregion Getters
	}
}
