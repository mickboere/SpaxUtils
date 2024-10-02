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
		public GameObject EquipedInstance { get; private set; }

		/// <summary>
		/// The see <see cref="IEquipmentData"/> (<see cref="IItemData"/>) of this equipment.
		/// </summary>
		public IEquipmentData EquipmentData => (IEquipmentData)RuntimeItemData.ItemData;

		/// <summary>
		/// The <see cref="IDependencyManager"/> belonging to this piece of equipment.
		/// </summary>
		public IDependencyManager DependencyManager { get; private set; }

		private List<DataStatMappingModifier> statModifiers = new List<DataStatMappingModifier>();
		private List<BehaviourAsset> behaviours = new List<BehaviourAsset>();

		public RuntimeEquipedData(RuntimeItemData runtimeItemData, IEquipmentSlot slot, IDependencyManager dependencyManager, GameObject equipedInstance = null)
		{
			RuntimeItemData = runtimeItemData;
			Slot = slot;
			DependencyManager = dependencyManager;
			EquipedInstance = equipedInstance;

			AddStatMappings();
		}

		public void Dispose()
		{
			foreach (DataStatMappingModifier mod in statModifiers)
			{
				mod.Dispose();
			}
			foreach (BehaviourAsset behaviour in behaviours)
			{
				behaviour.Destroy();
			}
		}

		/// <summary>
		/// Starts all equiped behaviours defined in the equipment data.
		/// </summary>
		public void InitializeBehaviour()
		{
			foreach (BehaviourAsset behaviour in EquipmentData.EquipedBehaviour)
			{
				BehaviourAsset behaviourInstance = behaviour.CreateInstance();
				behaviours.Add(behaviourInstance);
				DependencyManager.Inject(behaviourInstance);
				behaviourInstance.Start();
			}
		}

		/// <summary>
		/// Stops all running equipment behaviour.
		/// </summary>
		public void StopBehaviour()
		{
			foreach (BehaviourAsset behaviour in behaviours)
			{
				behaviour.Stop();
			}
		}

		private void AddStatMappings()
		{
			Agent agent = DependencyManager.Get<Agent>(true, false);

			foreach (StatMappingSheet statMappingSheet in EquipmentData.EquipedStatMappings)
			{
				foreach (StatMapping mapping in statMappingSheet.Mappings)
				{
					// Retrieve target stat to add mapping modifier to.
					EntityStat toStat = agent.GetStat(mapping.ToStat, true);
					// Generate unique mod identifier for this equipment in case multiple items utilize the same mapping.
					string identifier = GetModID(mapping.FromStat);

					if (!toStat.HasModifier(identifier))
					{
						if (RuntimeItemData.TryGetData(mapping.FromStat, out RuntimeDataEntry fromData))
						{
							DataStatMappingModifier mod = new DataStatMappingModifier(mapping, fromData);
							statModifiers.Add(mod);
							toStat.AddModifier(identifier, mod);
						}
						// The "from" data could not be found in item data.
						// No need to log since the StatMappingSheet can include mappings not relevant to this item.
					}
					else
					{
						SpaxDebug.Error($"Stat '{mapping.ToStat}' already contains a mapping from '{identifier}'.", "Mapping was not added.");
					}
				}
			}
		}

		private string GetModID(string itemStat)
		{
			return $"{ItemData.ID}_{itemStat}";
		}
	}
}
