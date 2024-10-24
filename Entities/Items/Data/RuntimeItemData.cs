﻿using SpaxUtils;
using SpaxUtils.StateMachines;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// Item data object instanced when an item is in an inventory.
	/// </summary>
	public class RuntimeItemData : IRuntimeItemData, IDisposable
	{
		public IItemData ItemData { get; private set; }
		public RuntimeDataCollection RuntimeData { get; private set; }

		public string RuntimeID => RuntimeData.ID;
		public string ItemID => ItemData.ID;

		public int Quantity => Unique ? 1 : RuntimeData.TryGetValue(ItemDataIdentifierConstants.QUANTITY, out int quantity) ? quantity : 1;

		public string Name => RuntimeData.GetValue<string>(ItemDataIdentifierConstants.NAME) ?? ItemData.Name;
		public string Description => RuntimeData.GetValue<string>(ItemDataIdentifierConstants.DESCRIPTION) ?? ItemData.Description;
		public string Category => RuntimeData.GetValue<string>(ItemDataIdentifierConstants.CATEGORY) ?? ItemData.Category;
		public bool Unique => RuntimeData.TryGetValue(ItemDataIdentifierConstants.UNIQUE, out bool unique) ? unique : ItemData.Unique;

		public IDependencyManager DependencyManager { get; private set; }

		private List<BehaviourAsset> behaviours = new List<BehaviourAsset>();

		public RuntimeItemData(IItemData itemData, RuntimeDataCollection runtimeData, IDependencyManager dependencyManager)
		{
			RuntimeData = runtimeData;
			ItemData = itemData;
			DependencyManager = dependencyManager;

			// Mandatory data used when loading item data.
			runtimeData.SetValue(ItemDataIdentifierConstants.ITEM_ID, ItemID);
		}

		/// <inheritdoc/>
		public bool TryGetData(string identifier, out RuntimeDataEntry data)
		{
			if (!RuntimeData.ContainsEntry(identifier))
			{
				if (!ItemData.FloatStats.ContainsKey(identifier))
				{
					data = null;
					return false;
				}

				data = new RuntimeDataEntry(identifier, ItemData.FloatStats[identifier]);
				return RuntimeData.TryAdd(data);
			}

			data = RuntimeData.GetEntry(identifier);
			return true;
		}

		/// <inheritdoc/>
		public bool TryGetStat(string identifier, out float value)
		{
			if (TryGetData(identifier, out RuntimeDataEntry data))
			{
				value = (float)data.Value;
				return true;
			}

			value = 0f;
			return false;
		}

		/// <summary>
		/// Starts all behaviours defined in the item data.
		/// </summary>
		public void InitializeBehaviour()
		{
			foreach (BehaviourAsset behaviour in ItemData.InventoryBehaviour)
			{
				BehaviourAsset behaviourInstance = behaviour.CreateInstance();
				behaviours.Add(behaviourInstance);
				DependencyManager.Inject(behaviourInstance);
				behaviourInstance.Start();
			}
		}

		/// <summary>
		/// Stops all running behaviours that were defined in the item data.
		/// Will also automatically happen if <see cref="Dispose"/> is called.
		/// </summary>
		public void StopBehaviour()
		{
			foreach (BehaviourAsset behaviour in behaviours)
			{
				behaviour.Stop();
			}
		}

		public void Dispose()
		{
			foreach (BehaviourAsset behaviour in behaviours)
			{
				behaviour.Destroy();
			}
		}

		public override string ToString()
		{
			return $"RuntimeItemData\n{{\n{ItemData}\n{RuntimeData}\n}}";
		}
	}
}
