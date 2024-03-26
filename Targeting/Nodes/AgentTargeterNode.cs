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

		private IEntity entity;
		private IActor actor;
		private ITargeter targeter;
		private IEntityCollection entityCollection;
		private AgentNavigationHandler navigationHandler;
		private CallbackService callbackService;

		private EntityComponentFilter<ITargetable> targetables;

		public void InjectDependencies(IEntity entity, IActor actor, ITargeter targeter,
			IEntityCollection entityCollection, AgentNavigationHandler navigationHandler,
			CallbackService callbackService)
		{
			this.entity = entity;
			this.actor = actor;
			this.targeter = targeter;
			this.entityCollection = entityCollection;
			this.navigationHandler = navigationHandler;
			this.callbackService = callbackService;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			actor.Listen<Act<bool>>(this, ActorActs.TARGET, OnTargetAct);
			targetables = new EntityComponentFilter<ITargetable>(entityCollection, (entity) => entity.Identification.HasAll(targetLabels), (c) => true, entity);
			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			actor.StopListening(this);
			targeter.SetTarget(null);
			targetables.Dispose();
		}

		private void OnUpdate()
		{
			if (targeter.Target != null && navigationHandler.Distance() > maxDistance)
			{
				targeter.SetTarget(null);
			}
		}

		private void OnTargetAct(Act<bool> act)
		{
			if (act.Value)
			{
				if (targeter.Target != null)
				{
					targeter.SetTarget(null);
				}
				else if (navigationHandler.TryGetClosestTargetable(targetables.Components, true, out ITargetable closest, out float distance) && distance < maxDistance)
				{
					// TODO: raycast?
					targeter.SetTarget(closest);
				}
			}
		}
	}
}
