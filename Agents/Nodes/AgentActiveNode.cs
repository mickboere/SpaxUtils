using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	public class AgentActiveNode : StateComponentNodeBase
	{
		private CallbackService callbackService;
		private AgentStatHandler statHandler;

		public void InjectDependencies(CallbackService callbackService, AgentStatHandler statHandler)
		{
			this.callbackService = callbackService;
			this.statHandler = statHandler;
		}

		public override void OnEnteringState(ITransition transition)
		{
			base.OnEnteringState(transition);
			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			callbackService.UnsubscribeUpdate(UpdateMode.Update, this);
		}

		private void OnUpdate(float delta)
		{
			statHandler.UpdateStats(delta);
		}
	}
}
