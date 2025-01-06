using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	public class AgentActiveNode : StateComponentNodeBase
	{
		private IAgent agent;
		private AgentStatHandler statHandler;

		public void InjectDependencies(IAgent agent, AgentStatHandler statHandler)
		{
			this.agent = agent;
			this.statHandler = statHandler;
		}

		public override void OnEnteringState(ITransition transition)
		{
			base.OnEnteringState(transition);
			agent.SubscribeOptimizedUpdate(OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			agent.UnsubscribeOptimizedUpdate(OnUpdate);
		}

		private void OnUpdate(float delta)
		{
			//SpaxDebug.Log($"[{agent.Identification.Name} | {agent.ID}] Update({agent.Priority}) [{delta.ToMilliseconds()}ms]");
			statHandler.UpdateStats(delta);
		}
	}
}
