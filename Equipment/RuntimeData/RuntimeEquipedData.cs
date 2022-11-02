using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Data generated at runtime for any equiped <see cref="IEquipmentData"/>.
	/// Contains references to all runtime elements belonging to this active equipment.
	/// </summary>
	public class RuntimeEquipedData : IDisposable
	{
		/// <summary>
		/// The <see cref="SpaxUtils.RuntimeItemData"/> this equiped data is paired to.
		/// </summary>
		public RuntimeItemData RuntimeItemData { get; private set; }

		/// <summary>
		/// Shortcut to <see cref="RuntimeItemData.ItemData"/>.
		/// </summary>
		public IItemData ItemData => RuntimeItemData.ItemData;

		/// <summary>
		/// The <see cref="IEquipmentSlot"/> this equipment is equiped in.
		/// </summary>
		public IEquipmentSlot Slot { get; private set; }

		/// <summary>
		/// The instantiated visual belonging to this equipment.
		/// </summary>
		public GameObject EquipedVisual { get; private set; }

		/// <summary>
		/// The see <see cref="IEquipmentData"/> (<see cref="IItemData"/>) of this equipment.
		/// </summary>
		public IEquipmentData EquipmentData => (IEquipmentData)RuntimeItemData.ItemData;

		/// <summary>
		/// The <see cref="IDependencyManager"/> belonging to this piece of equipment.
		/// </summary>
		public IDependencyManager DependencyManager { get; private set; }

		private List<BehaviorAsset> behaviors = new List<BehaviorAsset>();

		public RuntimeEquipedData(RuntimeItemData runtimeItemData, IEquipmentSlot slot, IDependencyManager dependencyManager, GameObject equipedVisual = null)
		{
			RuntimeItemData = runtimeItemData;
			Slot = slot;
			DependencyManager = dependencyManager;
			EquipedVisual = equipedVisual;
		}

		/// <summary>
		/// Starts all equiped behaviours defined in the equipment data.
		/// </summary>
		public void ExecuteBehaviour()
		{
			foreach (BehaviorAsset behaviour in EquipmentData.EquipedBehaviour)
			{
				BehaviorAsset behaviourInstance = behaviour.CreateInstance();
				behaviors.Add(behaviourInstance);
				DependencyManager.Inject(behaviourInstance);
				behaviourInstance.Start();
			}
		}

		/// <summary>
		/// Stops all running equipment behaviour.
		/// </summary>
		public void StopBehaviour()
		{
			foreach (BehaviorAsset behaviour in behaviors)
			{
				behaviour.Stop();
			}
		}

		/// <summary>
		/// Destroys all running equipment behaviour.
		/// </summary>
		public void Dispose()
		{
			foreach (BehaviorAsset behaviour in behaviors)
			{
				behaviour.Destroy();
			}
		}
	}
}
