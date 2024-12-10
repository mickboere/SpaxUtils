using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(HealingItemBehaviourAsset), menuName = "ScriptableObjects/Behaviours/" + nameof(HealingItemBehaviourAsset))]
	public class HealingItemBehaviourAsset : BehaviourAsset
	{
		private IAgent agent;
		private RuntimeEquipedData runtimeEquipedData;

		public void InjectDependencies(IAgent agent, RuntimeEquipedData runtimeEquipedData)
		{
			this.agent = agent;
			this.runtimeEquipedData = runtimeEquipedData;
		}
	}
}
