using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour that maps limb stats of equipment to the agent's limb stats.
	/// </summary>
	[CreateAssetMenu(fileName = "EquipmentLimbStatsBehaviour", menuName = "ScriptableObjects/EquipmentLimbStatsBehaviourAsset")]
	public class EquipmentLimbStatsBehaviourAsset : BehaviourAsset
	{
		[Serializable]
		public class SlotToLimb
		{
			public string SLOT => slot;
			public string LIMB => limb;

			[SerializeField, ConstDropdown(typeof(IEquipmentSlotTypeConstants))] private string slot;
			[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants), filter: AgentStatIdentifiers.SUB_STAT)] private string limb;
		}

		[SerializeField] private List<SlotToLimb> slotToLimbMap;
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private List<string> limbStats;

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

			foreach (string limbStat in limbStats)
			{
				if (runtimeEquipedData.RuntimeItemData.TryGetData(limbStat, out RuntimeDataEntry data) &&
					agent.TryGetStat(limbStat.SubStat(slotToLimbMap.First((m) => m.SLOT == runtimeEquipedData.Slot.Type).LIMB), out EntityStat stat))
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
