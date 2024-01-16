using System;
using System.Collections.Generic;
using System.Linq;
using SpaxUtils.StateMachine;
using SpiritAxis;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Node that applies all combat performance belonging to <see cref="CombatPerformerComponent"/> to the agent.
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
		private AgentArmsComponent arms;
		private IEntityCollection entityCollection;
		private AgentNavigationHandler navigationHandler;
		private ITargeter targeter;

		private FloatOperationModifier controlMod;
		private Dictionary<IPerformer, (PoserStruct pose, float weight)> poses = new Dictionary<IPerformer, (PoserStruct pose, float weight)>();
		private bool wasPerforming;
		private Timer momentumTimer;
		private bool appliedMomentum;
		private EntityComponentFilter<ITargetable> targetables;

		public void InjectDependencies(IAgent agent, IActor actor, ICombatPerformer combatPerformer, AnimatorPoser poser,
			IAgentMovementHandler movementHandler, RigidbodyWrapper rigidbodyWrapper, AgentArmsComponent arms,
			IEntityCollection entityCollection, AgentNavigationHandler navigationHandler, ITargeter targeter)
		{
			this.agent = agent;
			this.actor = actor;
			this.combatPerformer = combatPerformer;
			this.poser = poser;
			this.movementHandler = movementHandler;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.arms = arms;
			this.entityCollection = entityCollection;
			this.navigationHandler = navigationHandler;
			this.targeter = targeter;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			// Subscribe to events.
			combatPerformer.PerformanceUpdateEvent += OnPerformanceUpdateEvent;
			combatPerformer.PerformanceCompletedEvent += OnPerformanceCompletedEvent;
			combatPerformer.PoseUpdateEvent += OnPoseUpdateEvent;

			controlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			rigidbodyWrapper.Control.AddModifier(this, controlMod);
			arms.Weight.AddModifier(this, controlMod);

			targetables = new EntityComponentFilter<ITargetable>(entityCollection, (entity) => entity.Identification.HasAll(targetLabels), (c) => true, agent);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			// Return control to movement handler.
			rigidbodyWrapper.Control.RemoveModifier(this);
			arms.Weight.RemoveModifier(this);

			// Unsubscribe from events.
			combatPerformer.PerformanceUpdateEvent -= OnPerformanceUpdateEvent;
			combatPerformer.PerformanceCompletedEvent -= OnPerformanceCompletedEvent;
			combatPerformer.PoseUpdateEvent -= OnPoseUpdateEvent;

			// Clear data.
			controlMod.Dispose();
			targetables.Dispose();
			foreach (IPerformer performer in poses.Keys)
			{
				poser.RevokeInstructions(performer);
			}
			poses.Clear();

			wasPerforming = false;
		}

		private void OnPoseUpdateEvent(IPerformer performer, PoserStruct pose, float weight)
		{
			poses[performer] = (pose, weight);
		}

		private void OnPerformanceUpdateEvent(IPerformer performer)
		{
			var combatPerformer = performer as ICombatPerformer;
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
					if (combatPerformer.CurrentMove.PerformCost.Count > 0)
					{
						// Apply performance costs to stats.
						foreach (StatCost statCost in combatPerformer.CurrentMove.PerformCost)
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
					momentumTimer = new Timer(combatPerformer.CurrentMove.ForceDelay);
					appliedMomentum = false;
				}
				wasPerforming = performing;

				// Apply momentum to user after delay.
				if (performing && !appliedMomentum && !momentumTimer)
				{
					rigidbodyWrapper.AddImpactRelative(combatPerformer.CurrentMove.Inertia);
					appliedMomentum = true;
				}

				// Set control.
				float control = 1f - poses.Values.Select(v => v.weight).Sum().Clamp01();
				controlMod.SetValue(controlMod.Value < control ? Mathf.Lerp(controlMod.Value, control, controlWeightSmoothing * Time.deltaTime) : control);
			}

			// Set pose if performance isn't completed.
			if (performer.State != Performance.Completed)
			{
				poser.ProvideInstructions(performer, PoserLayerConstants.BODY, poses[performer].pose, 1, poses[performer].weight);
			}
		}

		private void OnPerformanceCompletedEvent(IPerformer performer)
		{
			poser.RevokeInstructions(performer);
			poses.Remove(performer);
			if (poses.Count == 0)
			{
				controlMod.SetValue(1f);
			}
		}
	}
}
