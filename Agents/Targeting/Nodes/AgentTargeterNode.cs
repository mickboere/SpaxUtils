using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	public class AgentTargeterNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField] private float maxDistance;

		private IAgent agent;
		private IEntityCollection entityCollection;
		private AgentNavigationHandler navigationHandler;
		private CallbackService callbackService;
		private IAgentMovementHandler movementHandler;
		private IVisionComponent visionComponent;

		public void InjectDependencies(IAgent agent, AgentNavigationHandler navigationHandler,
			CallbackService callbackService, IAgentMovementHandler movementHandler, IVisionComponent visionComponent)
		{
			this.agent = agent;
			this.navigationHandler = navigationHandler;
			this.callbackService = callbackService;
			this.movementHandler = movementHandler;
			this.visionComponent = visionComponent;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			agent.Actor.Listen<Act<bool>>(this, ActorActs.TARGET, OnTargetAct);
			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			agent.Actor.StopListening(this);
			agent.Targeter.SetTarget(null);
			callbackService.UnsubscribeUpdate(UpdateMode.Update, this);
			movementHandler.LockRotation = false;
		}

		private void OnUpdate()
		{
			if (agent.Targeter.Target != null && navigationHandler.Distance() > maxDistance)
			{
				agent.Targeter.SetTarget(null);
			}
		}

		private void OnTargetAct(Act<bool> act)
		{
			if (act.Value)
			{
				if (agent.Targeter.Target != null)
				{
					agent.Targeter.SetTarget(null);
				}
				else
				{
					ITargetable best = visionComponent.GetMostLikelyTarget(agent.Targeter.Enemies.Components);
					if (best != null)
					{
						agent.Targeter.SetTarget(best);
					}
				}
			}
		}
	}
}
