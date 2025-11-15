using SpaxUtils.StateMachines;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Activates the artificial emotional intelligence system with specified behaviours.
	/// </summary>
	[NodeWidth(300)]
	public class AEMOINode : StateComponentNodeBase
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
			agent.Mind.Activate(true);
			agent.SubscribeOptimizedUpdate(OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			agent.Mind.Deactivate();
			agent.Mind.RemoveBehaviours(behaviourInstances);
			agent.UnsubscribeOptimizedUpdate(OnUpdate);
		}

		private void OnUpdate(float delta)
		{
			agent.Mind.Update(delta);
		}
	}
}
