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

		private IVisionComponent spottingComponent;
		private IEntityCollection entityCollection;
		private IEntity entity;
		private ITargeter targeter;

		private EntityComponentFilter<ITargetable> targetables;

		public void InjectDependencies(IVisionComponent spottingComponent,
			IEntityCollection entityCollection, IEntity entity, ITargeter targeter)
		{
			this.spottingComponent = spottingComponent;
			this.entityCollection = entityCollection;
			this.entity = entity;
			this.targeter = targeter;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			targetables = new EntityComponentFilter<ITargetable>(entityCollection, entity);
			entity.SubscribeOptimizedUpdate(OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			entity.UnsubscribeOptimizedUpdate(OnUpdate);
			targetables.Dispose();
		}

		private void OnUpdate(float delta)
		{
			// TODO: Create separate component which keeps track of all things an agent has in view, instead of adding them as targetables.
			//List<ITargetable> spotted = spottingComponent.Spot(targetables.Components);
			//targeter.SetTargets(spotted);
		}
	}
}
