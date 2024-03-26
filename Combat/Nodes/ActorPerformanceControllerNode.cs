using System;
using System.Collections.Generic;
using System.Linq;
using SpaxUtils.StateMachines;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Node that controls pose performance of the <see cref="MovePerformerComponent"/>.
	/// </summary>
	public class ActorPerformanceControllerNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;

		private IActor actor;
		private IMovePerformer movePerformer;
		private AnimatorPoser poser;

		private Dictionary<IPerformer, (PoserStruct pose, float weight)> poses = new Dictionary<IPerformer, (PoserStruct pose, float weight)>();

		public void InjectDependencies(IActor actor, IMovePerformer combatPerformer, AnimatorPoser poser)
		{
			this.actor = actor;
			this.movePerformer = combatPerformer;
			this.poser = poser;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			// Subscribe to events.
			movePerformer.PerformanceUpdateEvent += OnPerformanceUpdateEvent;
			movePerformer.PerformanceCompletedEvent += OnPerformanceCompletedEvent;
			movePerformer.PoseUpdateEvent += OnPoseUpdateEvent;
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			// Force cancel current performance(s).
			actor.TryCancel(true);

			// Unsubscribe from events.
			movePerformer.PerformanceUpdateEvent -= OnPerformanceUpdateEvent;
			movePerformer.PerformanceCompletedEvent -= OnPerformanceCompletedEvent;
			movePerformer.PoseUpdateEvent -= OnPoseUpdateEvent;

			// Clear data.
			foreach (IPerformer performer in poses.Keys)
			{
				poser.RevokeInstructions(performer);
			}
			poses.Clear();
		}

		private void OnPoseUpdateEvent(IPerformer performer, PoserStruct pose, float weight)
		{
			poses[performer] = (pose, weight);
		}

		private void OnPerformanceUpdateEvent(IPerformer performer)
		{
			// Set pose if performance isn't completed.
			if (performer.State != PerformanceState.Completed)
			{
				poser.ProvideInstructions(performer, PoserLayerConstants.BODY, poses[performer].pose, 1, poses[performer].weight);
			}
		}

		private void OnPerformanceCompletedEvent(IPerformer performer)
		{
			poser.RevokeInstructions(performer);
			poses.Remove(performer);
		}
	}
}
