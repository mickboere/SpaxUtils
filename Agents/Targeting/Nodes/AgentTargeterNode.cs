using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	public class AgentTargeterNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;

		private IAgent agent;
		private AgentNavigationHandler navigationHandler;
		private IAgentMovementHandler movementHandler;
		private IVisionComponent visionComponent;
		private IEntityCollection entityCollection;

		public void InjectDependencies(IAgent agent, AgentNavigationHandler navigationHandler,
			IAgentMovementHandler movementHandler, IVisionComponent visionComponent, IEntityCollection entityCollection)
		{
			this.agent = agent;
			this.navigationHandler = navigationHandler;
			this.movementHandler = movementHandler;
			this.visionComponent = visionComponent;
			this.entityCollection = entityCollection;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			agent.Actor.Listen<Act<bool>>(this, ActorActs.TARGET, OnTargetAct);
			agent.SubscribeOptimizedUpdate(OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			agent.Actor.StopListening(this);
			agent.Targeter.SetTarget(null);
			agent.UnsubscribeOptimizedUpdate(OnUpdate);
			movementHandler.LockRotation = false;
		}

		private void OnUpdate(float delta)
		{
			if (agent.Targeter.Target != null &&
				(navigationHandler.Distance() > visionComponent.Range ||
					!agent.Targeter.Target.Entity.GameObject.activeInHierarchy))
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
					ITargetable best = visionComponent.GetMostLikelyTarget(entityCollection.GetComponents<ITargetable>(agent), true);
					if (best != null)
					{
						agent.Targeter.SetTarget(best);
					}
				}
			}
		}
	}
}
