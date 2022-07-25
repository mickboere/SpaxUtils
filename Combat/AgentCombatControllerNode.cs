using System;
using System.Collections.Generic;
using System.Linq;
using SpaxUtils.StateMachine;
using SpiritAxis;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Node that catches all <see cref="IAct"/>s coming through the <see cref="IActor"/> and tries to pass it to a <see cref="IPerformer"/>.
	/// </summary>
	public class AgentCombatControllerNode : StateMachineNodeBase
	{
		protected IPerformer Current => performers.Count > 0 ? performers[performers.Count - 1] : null;

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField] private float retryActionWindow;

		private IActor actor;
		private AnimatorPoser poser;
		private IAgentMovementHandler movementHandler;
		private RigidbodyWrapper rigidbodyWrapper;

		private List<IPerformer> performers = new List<IPerformer>();
		private Act<bool>? lastAct;
		private (Act<bool> act, Timer timer)? lastFailedAttempt;
		private bool wasPerforming;
		private FloatOperationModifier controlMod;

		public void InjectDependencies(IActor actor, AnimatorPoser poser, IAgentMovementHandler movementHandler, RigidbodyWrapper rigidbodyWrapper)
		{
			this.actor = actor;
			this.poser = poser;
			this.movementHandler = movementHandler;
			this.rigidbodyWrapper = rigidbodyWrapper;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			actor.PerformanceUpdateEvent += OnPerformanceUpdateEvent;
			actor.Listen<Act<bool>>(this, ActorActs.LIGHT, OnAct);
			actor.Listen<Act<bool>>(this, ActorActs.HEAVY, OnAct);

			controlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			rigidbodyWrapper.Control.AddModifier(this, controlMod);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			// Return control to movement handler.
			rigidbodyWrapper.Control.RemoveModifier(this);

			// Unsubscribe events.
			actor.PerformanceUpdateEvent -= OnPerformanceUpdateEvent;
			actor.StopListening(this);

			// Clear data.
			foreach (IPerformer performer in performers)
			{
				poser.RevokeInstructions(performer);
			}
			performers.Clear();

			lastAct = null;
			lastFailedAttempt = null;
			wasPerforming = false;
		}

		private void OnAct(Act<bool> act)
		{
			if (lastAct.HasValue && lastAct.Value.Value && lastAct.Value.Title != act.Title)
			{
				// Invalid input for current performance.
				return;
			}

			bool failed = false;
			if (act.Value)
			{
				if (actor.TryProduce(act, out IPerformer performer))
				{
					performers.Add(performer);
					wasPerforming = false;
				}
				else if (actor.Performing)
				{
					failed = true;
				}
			}
			else if (Current != null && !Current.TryPerform())
			{
				failed = true;
			}

			lastFailedAttempt = failed ? (act, new Timer(retryActionWindow)) : null;
			lastAct = act;
		}

		private void OnPerformanceUpdateEvent(IPerformer performer, PoserStruct pose, float weight)
		{
			//SpaxDebug.Log("OnPerformanceUpdateEvent", $"{performer.PerformanceTime}");

			if (!performers.Contains(performer))
			{
				return;
			}

			ICombatPerformer combatPerformer = performer as ICombatPerformer;

			bool performing = performer.PerformanceTime > 0f;

			// Only give control if it's the newest performance.
			if (performer == performers[performers.Count - 1])
			{
				controlMod.SetValue(performing ? 1f - weight : 0f);

				// Check if first frame of performance after charging.
				if (!wasPerforming && performing)
				{
					movementHandler.ForceRotation();
					rigidbodyWrapper.AddImpact(combatPerformer.Current.Impact);
				}
				wasPerforming = performing;
			}

			// Clean up if the performance has completed, set pose if not.
			if (performer.Completed)
			{
				poser.RevokeInstructions(performer);
				performers.Remove(performer);
				RetryLastFailedAttempt();
			}
			else
			{
				poser.ProvideInstructions(performer, PoserLayerConstants.BODY, pose, 1, weight);
			}
		}

		private void RetryLastFailedAttempt()
		{
			// If there was a failed action attempt within the last retryActionWindow OR of positive value, retry it.
			if (lastFailedAttempt.HasValue && (lastFailedAttempt.Value.act.Value || !lastFailedAttempt.Value.timer.Expired))
			{
				// If the last attempt was positive, only redo the positive as the input still needs to be released manually.
				// If the last attempt was negative, redo both positive and negative input to do a full performance.
				Act<bool> retry = lastFailedAttempt.Value.act;
				OnAct(new Act<bool>(retry.Title, true));
				if (!retry.Value)
				{
					OnAct(new Act<bool>(retry.Title, false));
				}

				lastFailedAttempt = null;
			}
		}
	}
}
