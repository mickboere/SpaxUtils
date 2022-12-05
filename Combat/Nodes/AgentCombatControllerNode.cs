﻿using System;
using System.Collections.Generic;
using System.Linq;
using SpaxUtils.StateMachine;
using SpiritAxis;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Node that catches all <see cref="IAct"/>s relating to combat and tries to pass them to an <see cref="IPerformer"/> for execution.
	/// </summary>
	public class AgentCombatControllerNode : StateMachineNodeBase
	{
		protected IPerformer Current => performers.Count > 0 ? performers[performers.Count - 1] : null;

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField] private float retryActionWindow;
		[SerializeField] private float controlWeightSmoothing = 6f;

		private IActor actor;
		private AnimatorPoser poser;
		private IAgentMovementHandler movementHandler;
		private RigidbodyWrapper rigidbodyWrapper;
		private IEquipmentComponent equipment;
		private ArmSlotsComponent armSlots;
		private CallbackService callbackService;
		private FloatOperationModifier controlMod;

		private List<IPerformer> performers = new List<IPerformer>();
		private Dictionary<IPerformer, float> weights = new Dictionary<IPerformer, float>();
		private Act<bool>? lastAct;
		private (Act<bool> act, Timer timer)? lastFailedAttempt;
		private bool wasPerforming;

		private RuntimeEquipedData leftEquip;
		private ArmedEquipmentComponent leftComp;
		private RuntimeEquipedData rightEquip;
		private ArmedEquipmentComponent rightComp;

		public void InjectDependencies(IActor actor, AnimatorPoser poser,
			IAgentMovementHandler movementHandler, RigidbodyWrapper rigidbodyWrapper,
			IEquipmentComponent equipment, ArmSlotsComponent armSlots, CallbackService callbackService)
		{
			this.actor = actor;
			this.poser = poser;
			this.movementHandler = movementHandler;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.equipment = equipment;
			this.armSlots = armSlots;
			this.callbackService = callbackService;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			// Subscribe to events.
			callbackService.LateUpdateCallback += OnLateUpdate;
			equipment.EquipedEvent += OnEquipedEvent;
			equipment.UnequipingEvent += OnUnquipingEvent;
			actor.PerformanceUpdateEvent += OnPerformanceUpdateEvent;
			actor.Listen<Act<bool>>(this, ActorActs.LIGHT, OnAct);
			actor.Listen<Act<bool>>(this, ActorActs.HEAVY, OnAct);

			controlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			rigidbodyWrapper.Control.AddModifier(this, controlMod);

			foreach (RuntimeEquipedData item in equipment.EquipedItems)
			{
				OnEquipedEvent(item);
			}
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			// Return control to movement handler.
			rigidbodyWrapper.Control.RemoveModifier(this);

			// Unsubscribe from events.
			callbackService.LateUpdateCallback -= OnLateUpdate;
			equipment.EquipedEvent -= OnEquipedEvent;
			equipment.UnequipingEvent -= OnUnquipingEvent;
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
			leftEquip = null;
			leftComp = null;
			rightEquip = null;
			rightComp = null;
		}

		private void OnLateUpdate()
		{
			if (leftComp != null)
			{
				armSlots.UpdateArm(true, controlMod.Value, leftComp.ArmedSettings, Time.deltaTime);
			}
			if (rightComp != null)
			{
				armSlots.UpdateArm(false, controlMod.Value, rightComp.ArmedSettings, Time.deltaTime);
			}
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
			if (!performers.Contains(performer))
			{
				return;
			}

			ICombatPerformer combatPerformer = performer as ICombatPerformer;
			bool performing = performer.PerformanceTime > 0f;
			weights[performer] = weight;

			// Only give control if it's the last performance.
			if (performer == performers[performers.Count - 1])
			{
				if (!wasPerforming && performing)
				{
					// First frame of performance.
					movementHandler.ForceRotation();
					rigidbodyWrapper.AddImpact(combatPerformer.Current.Impact);
				}
				wasPerforming = performing;

				if (combatPerformer.Finishing)
				{
					RetryLastFailedAttempt();
				}

				// Set control.
				float control = 1f - weights.Values.Sum().Clamp01();
				controlMod.SetValue(controlMod.Value < control ? Mathf.Lerp(controlMod.Value, control, controlWeightSmoothing * Time.deltaTime) : control);
			}

			// Clean up if the performance has completed, set pose if not.
			if (performer.Completed)
			{
				poser.RevokeInstructions(performer);
				performers.Remove(performer);
				weights.Remove(performer);
				if (weights.Count == 0)
				{
					controlMod.SetValue(1f);
				}
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
			}
		}

		private void OnEquipedEvent(RuntimeEquipedData data)
		{
			if (data.Slot.ID == HumanBoneIdentifiers.LEFT_HAND)
			{
				leftEquip = data;
				leftComp = leftEquip.EquipedVisual.GetComponent<ArmedEquipmentComponent>();
			}

			if (data.Slot.ID == HumanBoneIdentifiers.RIGHT_HAND)
			{
				rightEquip = data;
				rightComp = rightEquip.EquipedVisual.GetComponent<ArmedEquipmentComponent>();
			}
		}

		private void OnUnquipingEvent(RuntimeEquipedData data)
		{
			if (data == leftEquip)
			{
				leftEquip = null;
				leftComp = null;
			}

			if (data == rightEquip)
			{
				rightEquip = null;
				rightComp = null;
			}
		}
	}
}