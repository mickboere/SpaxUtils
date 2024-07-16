using SpaxUtils.StateMachines;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AEMOINode : StateComponentNodeBase
	{
		[SerializeField, Tooltip("Update rate in milliseconds.")] private int updateRate = 100;

		private IAgent agent;
		private CallbackService callbackService;
		private ITargeter targeter;

		public void InjectDependencies(IAgent agent, CallbackService callbackService, ITargeter targeter)
		{
			this.agent = agent;
			this.callbackService = callbackService;
			this.targeter = targeter;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			agent.Mind.Activate(true);
			callbackService.AddCustom(this, updateRate, OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			agent.Mind.Deactivate();
			callbackService.RemoveCustom(this);
		}

		private void OnUpdate(float delta)
		{
			UpdateMind(delta);
			ExecuteBehaviour();
		}

		private void UpdateMind(float delta)
		{
			agent.Mind.Update(delta);
		}

		private void ExecuteBehaviour()
		{
			// Gather the Mind's motivation and execute the most relevant behaviour.
			Vector8 motivation = agent.Mind.GetMotivation(out int index, out IEntity source);
			if (source != null)
			{
				targeter.SetTarget(source.GetEntityComponent<ITargetable>());
			}
		}
	}
}
