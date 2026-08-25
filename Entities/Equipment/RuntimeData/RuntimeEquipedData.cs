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
		/// The <see cref="WeaponComponent"/> on the <see cref="EquipedInstance"/>, or null for equipment that
		/// isn't a weapon. Resolved once and cached — combat queries it per candidate move per frame.
		/// </summary>
		public WeaponComponent Weapon
		{
			get
			{
				if (!weaponResolved)
				{
					weaponResolved = true;
					weapon = EquipedInstance == null ? null : EquipedInstance.GetComponentInChildren<WeaponComponent>();
				}
				return weapon;
			}
		}

		/// <summary>
		/// The <see cref="ICarryableItem"/> on the <see cref="EquipedInstance"/>, or null when it declares none.
		/// Resolved once and cached — sheathe points read it whenever a stack is re-laid-out.
		/// </summary>
		public ICarryableItem Carryable
		{
			get
			{
				if (!carryableResolved)
				{
					carryableResolved = true;
					carryable = EquipedInstance == null ? null : EquipedInstance.GetComponentInChildren<ICarryableItem>();
				}
				return carryable;
			}
		}

		/// <summary>
		/// The see <see cref="IEquipmentData"/> (<see cref="IItemData"/>) of this equipment.
		/// </summary>
		public IEquipmentData EquipmentData => (IEquipmentData)RuntimeItemData.ItemData;

		/// <summary>
		/// The <see cref="IDependencyManager"/> belonging to this piece of equipment.
		/// </summary>
		public IDependencyManager DependencyManager { get; private set; }

		/// <summary>
		/// Whether this equipment is actively in use, as opposed to merely carried.
		/// Arm slots are wielded by <see cref="AgentArmsComponent"/>; everything else wields on equip.
		/// </summary>
		public bool Wielded { get; private set; }

		private List<DataStatMappingModifier> statModifiers = new List<DataStatMappingModifier>();
		private List<(EntityStat stat, string modId)> physicsModifiers = new List<(EntityStat, string)>();
		private Dictionary<string, object> dataBackup = new Dictionary<string, object>();
		private List<BehaviourAsset> carriedBehaviours = new List<BehaviourAsset>();
		private List<BehaviourAsset> wieldedBehaviours = new List<BehaviourAsset>();
		private IEntity entity;
		private WeaponComponent weapon;
		private ICarryableItem carryable;
		private bool carryableResolved;
		private bool weaponResolved;

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
			Unwield();

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
			foreach (BehaviourAsset behaviour in carriedBehaviours)
			{
				behaviour.Destroy();
			}
			foreach (BehaviourAsset behaviour in wieldedBehaviours)
			{
				behaviour.Destroy();
			}
		}

		/// <summary>
		/// Instantiates both behaviour sets and starts the carried ones.
		/// Wielded behaviours stay dormant until <see cref="Wield"/>.
		/// </summary>
		public void InitializeBehaviour()
		{
			foreach (BehaviourAsset behaviour in EquipmentData.CarriedBehaviour)
			{
				carriedBehaviours.Add(CreateBehaviour(behaviour));
			}
			foreach (BehaviourAsset behaviour in EquipmentData.WieldedBehaviour)
			{
				wieldedBehaviours.Add(CreateBehaviour(behaviour));
			}

			foreach (BehaviourAsset behaviour in carriedBehaviours)
			{
				behaviour.Start();
			}
		}

		/// <summary>
		/// Stops all running equipment behaviour.
		/// </summary>
		public void StopBehaviour()
		{
			Unwield();

			foreach (BehaviourAsset behaviour in carriedBehaviours)
			{
				behaviour.Stop();
			}
		}

		/// <summary>
		/// Marks this equipment as actively in use, running its wielded behaviours.
		/// </summary>
		public void Wield()
		{
			if (Wielded)
			{
				return;
			}

			Wielded = true;
			foreach (BehaviourAsset behaviour in wieldedBehaviours)
			{
				behaviour.Start();
			}
		}

		/// <summary>
		/// Stows this equipment: it stays carried, but stops contributing anything that needs a hand.
		/// </summary>
		public void Unwield()
		{
			if (!Wielded)
			{
				return;
			}

			Wielded = false;
			foreach (BehaviourAsset behaviour in wieldedBehaviours)
			{
				behaviour.Stop();
			}
		}

		private BehaviourAsset CreateBehaviour(BehaviourAsset behaviour)
		{
			BehaviourAsset instance = behaviour.CreateInstance();
			DependencyManager.Inject(instance);
			return instance;
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
			if (!EquipmentData.PhysicsPassive)
			{
				return;
			}
			if (!DependencyManager.TryGet<AgentStatHandler>(out AgentStatHandler statHandler))
			{
				return;
			}

			for (int i = 0; i < 8; i++)
			{
				string physicId = statHandler.Physics.GetIdentifier(i);
				RuntimeDataEntry physicEntry = RuntimeItemData.RuntimeData.GetEntry(physicId);
				if (physicEntry == null)
				{
					continue;
				}
				EntityStat physicsStat = statHandler.Physics[i];
				string modId = GetModID($"physics_{i}");
				if (!physicsStat.HasModifier(modId))
				{
					DataStatMappingModifier mod = new(physicEntry, ModMethod.Additive, Operation.Add, () => (float)physicEntry.Value);
					statModifiers.Add(mod);
					physicsStat.AddModifier(modId, mod);
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
