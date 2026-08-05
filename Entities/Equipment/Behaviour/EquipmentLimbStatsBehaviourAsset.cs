using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour that maps limb stats of equipment to the agent's limb stats.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(EquipmentLimbStatsBehaviourAsset), menuName = "ScriptableObjects/Behaviours/" + nameof(EquipmentLimbStatsBehaviourAsset))]
	public class EquipmentLimbStatsBehaviourAsset : BehaviourAsset
	{
		[Serializable]
		public class SlotToLimb
		{
			public string Slot => slot;
			public string Limb => limb;

			[SerializeField, ConstDropdown(typeof(IEquipmentSlotTypeConstants))] private string slot;
			[SerializeField, ConstDropdown(typeof(IStatIdentifiers), filter: AgentStatIdentifiers.SUB_STAT)] private string limb;
		}

		[Serializable]
		public class ItemToAgent
		{
			public string ItemStat => itemStat;
			public string AgentStat => agentStat;

			[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers))] private string itemStat;
			[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers))] private string agentStat;
		}

		[SerializeField] private List<SlotToLimb> slotToLimbMap;
		[SerializeField] private List<ItemToAgent> itemToAgentMap;
		[SerializeField, Tooltip("Map the equipped weapon's own geometric reach (WeaponComponent, root to Tip) onto the acting limb. The weapon's physical shape is the source of truth, so reach can never disagree with what hit detection actually sweeps — and it follows the model rather than a hand-authored item value.")]
		private bool mapWeaponReach = true;
		[SerializeField, Conditional(nameof(mapWeaponReach)), ConstDropdown(typeof(ILabeledDataIdentifiers))] private string weaponReachStat;

		private IAgent agent;
		private RuntimeEquipedData runtimeEquipedData;
		private List<EntityStat> stats = new List<EntityStat>();

		public void InjectDependencies(IAgent agent, RuntimeEquipedData runtimeEquipedData)
		{
			this.agent = agent;
			this.runtimeEquipedData = runtimeEquipedData;
		}

		public override void Start()
		{
			base.Start();

			string limb = LimbFor(runtimeEquipedData.Slot.Type);
			if (string.IsNullOrEmpty(limb))
			{
				return;
			}

			foreach (ItemToAgent map in itemToAgentMap)
			{
				if (runtimeEquipedData.RuntimeItemData.RuntimeData.TryGetEntry(map.ItemStat, out RuntimeDataEntry data) &&
					agent.Stats.TryGetStat(map.AgentStat.SubStat(limb), out EntityStat stat))
				{
					var mod = new DataStatMappingModifier(data, ModMethod.Additive, Operation.Add, delegate () { return (float)data.Value; });
					stat.AddModifier(this, mod);
					stats.Add(stat);
				}
			}

			// Reach is read off the weapon's geometry rather than item data, so it stays in step with the model
			// the hit detector actually sweeps. Func-based so it tracks a weapon that is rescaled while held.
			if (mapWeaponReach && !string.IsNullOrEmpty(weaponReachStat))
			{
				WeaponComponent weapon = runtimeEquipedData.Weapon;
				if (weapon != null && agent.Stats.TryGetStat(weaponReachStat.SubStat(limb), out EntityStat reachStat))
				{
					reachStat.AddModifier(this, new FloatFuncModifier(ModMethod.Additive, (f) => f + weapon.Reach));
					stats.Add(reachStat);
				}
			}
		}

		/// <summary>
		/// The limb sub-stat driven by <paramref name="slotType"/>, or null when the slot maps to no limb.
		/// </summary>
		private string LimbFor(string slotType)
		{
			SlotToLimb map = slotToLimbMap.FirstOrDefault((m) => m.Slot == slotType);
			return map == null ? null : map.Limb;
		}

		public override void Stop()
		{
			base.Stop();

			foreach (EntityStat stat in stats)
			{
				stat.RemoveModifier(this);
			}
			stats.Clear();
		}
	}
}
