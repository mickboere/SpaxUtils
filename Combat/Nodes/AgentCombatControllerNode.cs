using System;
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
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField] private float controlWeightSmoothing = 6f;
		[SerializeField, ConstDropdown(typeof(IIdentificationLabels))] private string[] targetLabels;
		[SerializeField] private float autoAimRange = 2f;

		private IAgent agent;
		private IActor actor;
		private ICombatPerformer combatPerformer;
		private AnimatorPoser poser;
		private IAgentMovementHandler movementHandler;
		private RigidbodyWrapper rigidbodyWrapper;
		private IEquipmentComponent equipment;
		private AgentArmsComponent armSlots;
		private CallbackService callbackService;
		private IEntityCollection entityCollection;
		private AgentNavigationHandler navigationHandler;
		private ITargeter targeter;

		private FloatOperationModifier controlMod;
		private Dictionary<IPerformer, (PoserStruct pose, float weight)> poses = new Dictionary<IPerformer, (PoserStruct pose, float weight)>();
		private bool wasPerforming;
		private Timer momentumTimer;
		private bool appliedMomentum;
		private EntityComponentFilter<ITargetable> targetables;

		private RuntimeEquipedData leftEquip;
		private ArmedEquipmentComponent leftComp;
		private RuntimeEquipedData rightEquip;
		private ArmedEquipmentComponent rightComp;

		public void InjectDependencies(IAgent agent, IActor actor, ICombatPerformer combatPerformer, AnimatorPoser poser,
			IAgentMovementHandler movementHandler, RigidbodyWrapper rigidbodyWrapper,
			IEquipmentComponent equipment, AgentArmsComponent armSlots, CallbackService callbackService,
			IEntityCollection entityCollection, AgentNavigationHandler navigationHandler, ITargeter targeter)
		{
			this.agent = agent;
			this.actor = actor;
			this.combatPerformer = combatPerformer;
			this.poser = poser;
			this.movementHandler = movementHandler;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.equipment = equipment;
			this.armSlots = armSlots;
			this.callbackService = callbackService;
			this.entityCollection = entityCollection;
			this.navigationHandler = navigationHandler;
			this.targeter = targeter;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			// Subscribe to events.
			callbackService.LateUpdateCallback += OnLateUpdate;
			equipment.EquipedEvent += OnEquipedEvent;
			equipment.UnequipingEvent += OnUnquipingEvent;
			actor.PerformanceUpdateEvent += OnPerformanceUpdateEvent;
			combatPerformer.PoseUpdateEvent += OnPoseUpdateEvent;

			controlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			rigidbodyWrapper.Control.AddModifier(this, controlMod);

			targetables = new EntityComponentFilter<ITargetable>(entityCollection, (entity) => entity.Identification.HasAll(targetLabels), (c) => true, agent);

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
			combatPerformer.PoseUpdateEvent -= OnPoseUpdateEvent;
			actor.StopListening(this);

			// Clear data.
			targetables.Dispose();
			foreach (IPerformer performer in poses.Keys)
			{
				poser.RevokeInstructions(performer);
			}
			poses.Clear();

			wasPerforming = false;
			leftEquip = null;
			leftComp = null;
			rightEquip = null;
			rightComp = null;

			armSlots.ResetArms();
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

		private void OnPoseUpdateEvent(IPerformer performer, PoserStruct pose, float weight)
		{
			poses[performer] = (pose, weight);
		}

		private void OnPerformanceUpdateEvent(IPerformer performer)
		{
			if (performer is not ICombatPerformer combatPerformer)
			{
				return;
			}

			bool performing = performer.RunTime > 0f;

			// Only give control if it's the main performance.
			if (performer == actor.MainPerformer)
			{
				if (!wasPerforming && performing)
				{
					// < First frame of performance >

					#region Aiming
					if (targeter.Target != null)
					{
						// Auto aim to target.
						movementHandler.SetTargetVelocity((targeter.Target.Center - rigidbodyWrapper.Position).normalized);
					}
					else if (rigidbodyWrapper.TargetVelocity.magnitude <= 1f &&
						navigationHandler.TryGetClosestTargetable(targetables.Components, false, out ITargetable closest, out float distance) &&
						distance < autoAimRange)
					{
						// Auto aim to closest targetable in range.
						movementHandler.SetTargetVelocity((closest.Center - rigidbodyWrapper.Position).normalized);
					}
					#endregion Aiming

					#region Stats
					if (combatPerformer.Current.PerformCost.Count > 0)
					{
						// Apply performance costs to stats.
						foreach (StatCost statCost in combatPerformer.Current.PerformCost)
						{
							if (agent.TryGetStat(statCost.Stat, out EntityStat costStat))
							{
								float cost = statCost.Cost;
								if (statCost.Multiply && agent.TryGetStat(statCost.Multiplier, out EntityStat multiplier))
								{
									cost *= multiplier;
								}
								costStat.BaseValue -= cost;
							}
						}
					}
					#endregion Stats

					movementHandler.ForceRotation();
					momentumTimer = new Timer(combatPerformer.Current.ForceDelay);
					appliedMomentum = false;
				}
				wasPerforming = performing;

				// Apply momentum to user after delay.
				if (performing && !appliedMomentum && !momentumTimer)
				{
					rigidbodyWrapper.AddImpactRelative(combatPerformer.Current.Inertia);
					appliedMomentum = true;
				}

				// Set control.
				float control = 1f - poses.Values.Select(v => v.weight).Sum().Clamp01();
				controlMod.SetValue(controlMod.Value < control ? Mathf.Lerp(controlMod.Value, control, controlWeightSmoothing * Time.deltaTime) : control);
			}

			// Clean up if the performance has completed, set pose if not.
			if (performer.Completed)
			{
				poser.RevokeInstructions(performer);
				poses.Remove(performer);
				if (poses.Count == 0)
				{
					controlMod.SetValue(1f);
				}
			}
			else
			{
				poser.ProvideInstructions(performer, PoserLayerConstants.BODY, poses[performer].pose, 1, poses[performer].weight);
			}
		}

		private void OnEquipedEvent(RuntimeEquipedData data)
		{
			if (data.Slot.ID == HumanBoneIdentifiers.LEFT_HAND)
			{
				leftEquip = data;
				leftComp = leftEquip.EquipedInstance.GetComponent<ArmedEquipmentComponent>();
			}

			if (data.Slot.ID == HumanBoneIdentifiers.RIGHT_HAND)
			{
				rightEquip = data;
				rightComp = rightEquip.EquipedInstance.GetComponent<ArmedEquipmentComponent>();
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
