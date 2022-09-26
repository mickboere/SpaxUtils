﻿using SpaxUtils;
using SpaxUtils.StateMachine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// Item data object instanced when an item is in an inventory.
	/// </summary>
	public class RuntimeItemData : IDisposable
	{
		public IItemData ItemData { get; private set; }
		public RuntimeDataCollection RuntimeData { get; private set; }

		public string RuntimeID => RuntimeData.UID;
		public string ItemID => ItemData.UID;

		public int Quantity => Unique ? 1 : RuntimeData.TryGet(ItemDataIdentifierConstants.QUANTITY, out int quantity) ? quantity : 1;

		public string Name => RuntimeData.Get<string>(ItemDataIdentifierConstants.NAME) ?? ItemData.Name;
		public string Description => RuntimeData.Get<string>(ItemDataIdentifierConstants.DESCRIPTION) ?? ItemData.Description;
		public string Category => RuntimeData.Get<string>(ItemDataIdentifierConstants.CATEGORY) ?? ItemData.Category;
		public bool Unique => RuntimeData.TryGet(ItemDataIdentifierConstants.UNIQUE, out bool unique) ? unique : ItemData.Unique;

		public IDependencyManager DependencyManager { get; private set; }

		private List<BehaviourAsset> behaviours;

		public RuntimeItemData(RuntimeDataCollection runtimeData, IItemData itemData, IDependencyManager dependencyManager)
		{
			RuntimeData = runtimeData;
			ItemData = itemData;
			DependencyManager = dependencyManager;

			// We only rely on the item ID being present in the data upon instancing.
			runtimeData.Set(ItemDataIdentifierConstants.ITEM_ID, itemData.UID);
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
