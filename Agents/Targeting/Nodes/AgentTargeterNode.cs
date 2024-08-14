using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	public class AgentTargeterNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField, ConstDropdown(typeof(IIdentificationLabels))] private string[] targetLabels;
		[SerializeField] private float maxDistance;

		private IAgent agent;
		private IEntityCollection entityCollection;
		private AgentNavigationHandler navigationHandler;
		private CallbackService callbackService;
		private IAgentMovementHandler movementHandler;

		private EntityComponentFilter<ITargetable> targetables;

		public void InjectDependencies(IAgent agent,
			IEntityCollection entityCollection, AgentNavigationHandler navigationHandler,
			CallbackService callbackService, IAgentMovementHandler movementHandler)
		{
			this.agent = agent;
			this.entityCollection = entityCollection;
			this.navigationHandler = navigationHandler;
			this.callbackService = callbackService;
			this.movementHandler = movementHandler;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			agent.Actor.Listen<Act<bool>>(this, ActorActs.TARGET, OnTargetAct);
			targetables = new EntityComponentFilter<ITargetable>(entityCollection, (agent) => agent.Identification.HasAll(targetLabels), (c) => true, agent);
			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			agent.Actor.StopListening(this);
			agent.Targeter.SetTarget(null);
			targetables.Dispose();
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
				else if (navigationHandler.TryGetClosestTarget(targetables.Components, out ITargetable closest, out float distance) && distance < maxDistance)
				{
					// TODO: raycast?
					agent.Targeter.SetTarget(closest);
				}
			}
		}
	}
}
