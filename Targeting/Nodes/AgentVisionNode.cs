using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	public class AgentVisionNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField, Tooltip("Spotting interval in seconds.")] private float interval = 1f;

		private ISpottingComponent spottingComponent;
		private CallbackService callbackService;
		private IEntityCollection entityCollection;
		private IEntity entity;
		private ITargeter targeter;

		private EntityComponentFilter<ITargetable> targetables;

		public void InjectDependencies(ISpottingComponent spottingComponent, CallbackService callbackService,
			IEntityCollection entityCollection, IEntity entity, ITargeter targeter)
		{
			this.spottingComponent = spottingComponent;
			this.callbackService = callbackService;
			this.entityCollection = entityCollection;
			this.entity = entity;
			this.targeter = targeter;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			targetables = new EntityComponentFilter<ITargetable>(entityCollection, entity);
			callbackService.AddCustom(this, interval, OnUpdateCallback);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			callbackService.RemoveCustom(this);
			targetables.Dispose();
		}

		private void OnUpdateCallback()
		{
			// TODO: Create separate component which keeps track of all things an agent has in view, instead of adding them as targetables.
			//List<ITargetable> spotted = spottingComponent.Spot(targetables.Components);
			//targeter.SetTargets(spotted);
		}
	}
}
