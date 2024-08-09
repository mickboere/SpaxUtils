using SpaxUtils.StateMachines;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[NodeWidth(300)]
	public class AEMOINode : StateComponentNodeBase
	{
		[SerializeField, Tooltip("Update rate in milliseconds.")] private int updateRate = 100;
		[SerializeField] private List<AEMOIBehaviourAsset> behaviours;

		private IAgent agent;
		private CallbackService callbackService;
		private ITargeter targeter;

		private List<AEMOIBehaviourAsset> behaviourInstances;

		public void InjectDependencies(IAgent agent, CallbackService callbackService, ITargeter targeter)
		{
			this.agent = agent;
			this.callbackService = callbackService;
			this.targeter = targeter;
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
			callbackService.AddCustom(this, updateRate, OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			agent.Mind.Deactivate();
			agent.Mind.RemoveBehaviours(behaviourInstances);
			callbackService.RemoveCustom(this);
		}

		private void OnUpdate(float delta)
		{
			UpdateMind(delta);
		}

		private void UpdateMind(float delta)
		{
			agent.Mind.Update(delta);
		}
	}
}
