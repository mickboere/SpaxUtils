using SpaxUtils.StateMachines;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[NodeWidth(300)]
	public class AEMOIBehaviourNode : StateComponentNodeBase
	{
		[SerializeField] private List<AEMOIBehaviourAsset> behaviours;

		private IAgent agent;

		private List<AEMOIBehaviourAsset> behaviourInstances;

		public void InjectDependencies(IAgent agent)
		{
			this.agent = agent;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			if (behaviourInstances == null)
			{
				behaviourInstances = new List<AEMOIBehaviourAsset>();
				foreach (AEMOIBehaviourAsset asset in behaviours)
				{
					behaviourInstances.Add(Instantiate(asset));
				}
			}

			agent.Mind.AddBehaviours(behaviourInstances);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			agent.Mind.RemoveBehaviours(behaviourInstances);
		}
	}
}
