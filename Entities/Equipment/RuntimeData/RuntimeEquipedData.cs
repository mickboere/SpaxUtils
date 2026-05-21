using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Data generated at runtime for any equiped <see cref="IEquipmentData"/>.
	/// Contains references to all runtime elements belonging to this active equipment.
	/// </summary>
	public class RuntimeEquipedData : IRuntimeDataContainer, IDisposable
	{
		/// <summary>
		/// The <see cref="SpaxUtils.RuntimeItemData"/> this equiped data is paired to.
		/// </summary>
		public RuntimeItemData RuntimeItemData { get; private set; }

		/// <summary>
		/// The <see cref="RuntimeDataCollection"/> belonging to the <see cref="RuntimeItemData"/>.
		/// </summary>
		public RuntimeDataCollection RuntimeData => RuntimeItemData.RuntimeData;

		/// <summary>
		/// Shortcut to <see cref="RuntimeItemData.ItemData"/>.
		/// </summary>
		public IItemData ItemData => RuntimeItemData.ItemData;

		/// <summary>
		/// Shortcut to <see cref="RuntimeItemData.ItemData"/>, but as <see cref="IEquipmentData"/>.
		/// </summary>
		public IEquipmentData EquipmentItemData => (IEquipmentData)ItemData;

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
		private List<(EntityStat stat, string modId)> physicsModifiers = new List<(EntityStat, string)>();
		private Dictionary<string, object> dataBackup = new Dictionary<string, object>();
		private List<BehaviourAsset> behaviours = new List<BehaviourAsset>();
		private IEntity entity;

		public RuntimeEquipedData(RuntimeItemData runtimeItemData, IEquipmentSlot slot, IDependencyManager dependencyManager, IEntity entity, GameObject equipedInstance = null)
		{
			RuntimeItemData = runtimeItemData;
			Slot = slot;
			DependencyManager = dependencyManager;
			this.entity = entity;
			EquipedInstance = equipedInstance;

			AddStatMappings();
			AddPhysicsPassiveMappings();

			// Apply material overrides (if any) to the equiped instance.
			// Rules are enforced inside MaterialOverride.ApplyOverrides().
			if (EquipedInstance != null)
			{
				MaterialOverride.ApplyOverrides(EquipedInstance, EquipmentData.MaterialOverrides);
			}
		}

		public void Dispose()
		{
			foreach (DataStatMappingModifier mod in statModifiers)
			{
				mod.Dispose();
			}
			foreach ((EntityStat stat, string modId) in physicsModifiers)
			{
				stat.RemoveModifier(modId);
			}
			foreach (KeyValuePair<string, object> backup in dataBackup)
			{
				entity.RuntimeData.SetValue(backup.Key, backup.Value);
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
			foreach (RuntimeDataEntry data in RuntimeItemData.RuntimeData.Data)
			{
				// Mass → Load: intrinsic mapping, no EquipMap required.
				if (data.ID == ItemDataIdentifiers.MASS)
				{
					EntityStat loadStat = entity.Stats.GetStat(AgentStatIdentifiers.LOAD, true);
					string modId = GetModID(data.ID);
					if (!loadStat.HasModifier(modId))
					{
						DataStatMappingModifier mod = new(data, ModMethod.Additive, Operation.Add, () => (float)data.Value);
						statModifiers.Add(mod);
						loadStat.AddModifier(modId, mod);
					}
					//continue; // Mass is handled directly above; skip EquipMap processing for this entry.
				}

				foreach (StatMap statMappingSheet in EquipmentData.EquipedStatMappings)
				{
					// This allows one item stat (e.g. "Strength") to modify multiple entity stats (e.g. "Power" AND "CarryWeight")
					foreach (StatMapping mapping in statMappingSheet.GetMappingsFrom(data.ID))
					{
						// STAT MOD MAPPING:
						// Retrieve target stat to add mapping modifier to.
						EntityStat toStat = entity.Stats.GetStat(mapping.ToStat, true);

						// Generate unique mod identifier.
						// (Note: Since this identifier is scoped to the 'toStat', using FromStat name is safe even for 1-to-Many mappings)
						string identifier = GetModID(mapping.FromStat);

						// If mod isn't present yet, add it to stat.
						if (!toStat.HasModifier(identifier))
						{
							DataStatMappingModifier mod = new DataStatMappingModifier(mapping, data);
							statModifiers.Add(mod);
							toStat.AddModifier(identifier, mod);
						}
						else
						{
							SpaxDebug.Error($"Stat '{mapping.ToStat}' already contains a mapping from '{identifier}'.", $"Mapping was not added. Source map: '{statMappingSheet.name}' on item '{ItemData.ID}'.");
						}
					}

					// DIRECT DATA MAPPING (Keep existing logic, usually 1-to-1)
					if (statMappingSheet.DataMappings.Contains(data.ID))
					{
						dataBackup[data.ID] = entity.RuntimeData.GetValue(data.ID, data.ValueType.GetDefault());
						entity.RuntimeData.SetValue(data.ID, data.Value, true, false);
					}
				}
			}
		}

		private void AddPhysicsPassiveMappings()
		{
			if (!EquipmentData.PhysicsPassive) return;
			if (!DependencyManager.TryGet<AgentStatHandler>(out AgentStatHandler statHandler)) return;

			Vector8 distribution = EquipmentData.PhysicsDistribution;
			float scaling = EquipmentData.PhysicsScaling;
			for (int i = 0; i < 8; i++)
			{
				float contribution = distribution[i] * scaling;
				if (contribution == 0f) continue;
				EntityStat physicsStat = statHandler.Physics[i];
				string modId = GetModID($"physics_{i}");
				if (!physicsStat.HasModifier(modId))
				{
					physicsStat.AddModifier(modId, new FloatOperationModifier(ModMethod.Additive, Operation.Add, contribution));
					physicsModifiers.Add((physicsStat, modId));
				}
			}
		}

		private string GetModID(string itemStat)
		{
			return $"{ItemData.ID}_{itemStat}";
		}
	}
}
