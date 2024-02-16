using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour for melee <see cref="ICombatMove"/>s that manages hit-detection during performance.
	/// </summary>
	[CreateAssetMenu(fileName = "Behaviour_MeleeAttack", menuName = "ScriptableObjects/Combat/MeleeAttackBehaviourAsset")]
	public class MeleeAttackingBehaviourAsset : BasePerformanceMoveBehaviourAsset
	{
		[SerializeField, ConstDropdown(typeof(IIdentificationLabels))] private string[] targetLabels;
		[SerializeField] private float autoAimRange = 2f;
		[SerializeField] private LayerMask hitDetectionMask;

		protected ICombatMove combatMove;
		private CallbackService callbackService;
		private TransformLookup transformLookup;
		private ITargeter targeter;
		private IAgentMovementHandler movementHandler;
		private AgentNavigationHandler navigationHandler;
		private IEntityCollection entityCollection;
		private CombatSettings combatSettings;

		private EntityStat timescaleStat;
		private EntityStat massStat;
		private EntityStat strengthStat;
		private EntityStat offenceStat;
		private EntityStat piercingStat;

		private CombatHitDetector hitDetector;
		private bool wasPerforming;
		private TimerStruct momentumTimer;
		private bool appliedMomentum;
		private EntityComponentFilter<ITargetable> targetables;
		private TimedCurveModifier hitPauseMod;

		public void InjectDependencies(ICombatMove move, CallbackService callbackService,
			TransformLookup transformLookup, ITargeter targeter, IAgentMovementHandler movementHandler,
			AgentNavigationHandler navigationHandler, IEntityCollection entityCollection, CombatSettings combatSettings)
		{
			this.combatMove = move;
			this.callbackService = callbackService;
			this.transformLookup = transformLookup;
			this.targeter = targeter;
			this.movementHandler = movementHandler;
			this.navigationHandler = navigationHandler;
			this.entityCollection = entityCollection;
			this.combatSettings = combatSettings;

			timescaleStat = Agent.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);
			massStat = Agent.GetStat(AgentStatIdentifiers.MASS.SubStat(combatMove.Limb));
			strengthStat = Agent.GetStat(AgentStatIdentifiers.STRENGTH);
			offenceStat = Agent.GetStat(AgentStatIdentifiers.OFFENCE.SubStat(combatMove.Limb));
			piercingStat = Agent.GetStat(AgentStatIdentifiers.PIERCING.SubStat(combatMove.Limb));
		}

		public override void Start()
		{
			base.Start();

			hitDetector = new CombatHitDetector(Agent, transformLookup, combatMove, hitDetectionMask);
			targetables = new EntityComponentFilter<ITargetable>(entityCollection, (entity) => entity.Identification.HasAll(targetLabels), (c) => true, Agent);
		}

		public override void Stop()
		{
			base.Stop();

			hitDetector.Dispose();
			targetables.Dispose();
		}

		public override void CustomUpdate(float delta)
		{
			base.CustomUpdate(delta);

			if (Performer.State == PerformanceState.Performing)
			{
				if (!wasPerforming)
				{
					OnFirstFrameOfPerformance();
					wasPerforming = true;
				}

				if (Performer.RunTime >= combatMove.HitDetectionDelay && hitDetector.Update(out List<HitScanHitData> newHits))
				{
					OnNewHitDetected(newHits);
				}

				// Apply momentum to user after delay.
				if (!appliedMomentum && !momentumTimer)
				{
					RigidbodyWrapper.AddImpactRelative(combatMove.Inertia);
					appliedMomentum = true;
				}
			}
		}

		protected void OnFirstFrameOfPerformance()
		{
			// Aiming.
			if (targeter.Target != null)
			{
				// Auto aim to target.
				movementHandler.SetTargetVelocity((targeter.Target.Center - RigidbodyWrapper.Position).normalized);
			}
			else if (RigidbodyWrapper.TargetVelocity.magnitude <= 1f &&
				navigationHandler.TryGetClosestTargetable(targetables.Components, false, out ITargetable closest, out float distance) &&
				distance < autoAimRange)
			{
				// Auto aim to closest targetable in range.
				movementHandler.SetTargetVelocity((closest.Center - RigidbodyWrapper.Position).normalized);
			}
			movementHandler.ForceRotation();

			// Stats.
			if (combatMove.PerformCost.Count > 0)
			{
				// Performance cost.
				foreach (StatCost statCost in combatMove.PerformCost)
				{
					if (Agent.TryGetStat(statCost.Stat, out EntityStat costStat))
					{
						float cost = statCost.Cost;
						if (statCost.Multiply && Agent.TryGetStat(statCost.Multiplier, out EntityStat multiplier))
						{
							cost *= multiplier;
						}
						costStat.BaseValue -= cost;
					}
				}
			}

			momentumTimer = new TimerStruct(combatMove.ForceDelay);
			appliedMomentum = false;
		}

		protected void OnNewHitDetected(List<HitScanHitData> newHits)
		{
			foreach (HitScanHitData hit in newHits)
			{
				if (hit.GameObject.TryGetComponentRelative(out IHittable hittable))
				{
					// Generate hit data.
					Vector3 inertia = combatMove.Inertia.Look((hittable.Entity.Transform.position - Agent.Transform.position).FlattenY().normalized);
					float mass = massStat;
					float strength = strengthStat * combatMove.Strength;
					float offence = offenceStat * combatMove.Offence;
					float piercing = piercingStat * combatMove.Piercing;

					HitData hitData = new HitData(
						hittable,
						Agent,
						inertia,
						hit.Direction,
						mass,
						strength,
						offence,
						piercing
					);

					ProcessAttack(hittable, hitData);
				}
			}
		}

		protected virtual void ProcessAttack(IHittable hittable, HitData hitData)
		{
			// Send hit and apply return data.
			if (hittable.Hit(hitData))
			{
				if (hitData.Parried)
				{
					Performer.TryCancel(true);
				}

				float hitPause = combatSettings.MaxHitPause * hitData.Penetration.InvertClamped();
				if (hitPauseMod == null || hitPause > hitPauseMod.Timer.Remaining)
				{
					// Apply hit-pause.
					hitPauseMod?.Dispose();
					hitPauseMod = new TimedCurveModifier(
						ModMethod.Absolute,
						combatSettings.HitPauseCurve,
						new TimerStruct(hitPause),
						callbackService);
					timescaleStat.RemoveModifier(this);
					timescaleStat.AddModifier(this, hitPauseMod);
				}
			}
		}
	}
}
