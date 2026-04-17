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

			foreach (ItemToAgent map in itemToAgentMap)
			{
				if (runtimeEquipedData.RuntimeItemData.RuntimeData.TryGetEntry(map.ItemStat, out RuntimeDataEntry data) &&
					agent.Stats.TryGetStat(map.AgentStat.SubStat(slotToLimbMap.First((m) => m.Slot == runtimeEquipedData.Slot.Type).Limb), out EntityStat stat))
				{
					var mod = new DataStatMappingModifier(data, ModMethod.Additive, Operation.Add, delegate () { return (float)data.Value; });
					stat.AddModifier(this, mod);
					stats.Add(stat);
				}
			}
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
