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
		[SerializeField] private float hitPause = 0.2f;
		[SerializeField] private AnimationCurve hitPauseCurve;

		private ICombatMove combatMove;
		private CallbackService callbackService;
		private TransformLookup transformLookup;
		private ITargeter targeter;
		private IAgentMovementHandler movementHandler;
		private AgentNavigationHandler navigationHandler;
		private IEntityCollection entityCollection;

		private CombatHitDetector hitDetector;
		private EntityStat entityTimeScale;
		private TimedCurveModifier timeMod;

		private EntityStat strengthStat;
		private EntityStat offenceStat;
		private EntityStat piercingStat;

		private bool wasPerforming;
		private Timer momentumTimer;
		private bool appliedMomentum;
		private EntityComponentFilter<ITargetable> targetables;

		public void InjectDependencies(ICombatMove move, CallbackService callbackService,
			TransformLookup transformLookup, ITargeter targeter, IAgentMovementHandler movementHandler,
			AgentNavigationHandler navigationHandler, IEntityCollection entityCollection)
		{
			this.combatMove = move;
			this.callbackService = callbackService;
			this.transformLookup = transformLookup;
			this.targeter = targeter;
			this.movementHandler = movementHandler;
			this.navigationHandler = navigationHandler;
			this.entityCollection = entityCollection;

			entityTimeScale = Agent.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);
			strengthStat = Agent.GetStat(AgentStatIdentifiers.STRENGTH);
			offenceStat = Agent.GetStat(AgentStatIdentifiers.OFFENCE);
			piercingStat = Agent.GetStat(AgentStatIdentifiers.PIERCING, true);
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

			movementHandler.ForceRotation();
			momentumTimer = new Timer(combatMove.ForceDelay);
			appliedMomentum = false;
		}

		private void OnNewHitDetected(List<HitScanHitData> newHits)
		{
			// TODO: Have hit pause duration depend on penetration % (factoring in power, sharpness, hardness...)
			timeMod = new TimedCurveModifier(ModMethod.Absolute, hitPauseCurve, new Timer(hitPause), callbackService);

			bool successfulHit = false;
			foreach (HitScanHitData hit in newHits)
			{
				if (hit.GameObject.TryGetComponentRelative(out IHittable hittable))
				{
					Vector3 inertia = combatMove.Inertia.Look((hittable.Entity.Transform.position - Agent.Transform.position).FlattenY());

					// Calculate hit data.
					float mass = RigidbodyWrapper.Mass * combatMove.MassInfluence;
					float strength = strengthStat * combatMove.Strength;
					float offence = offenceStat * combatMove.Offensiveness;
					float piercing = piercingStat * combatMove.Piercing;

					// Generate hit-data for hittable.
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

					// TODO: Create IHitter class where other classes can add damages to the hitdata.

					if (hittable.Hit(hitData))
					{
						successfulHit = true;

						// Apply hit pause to enemy.
						// TODO: Must be applied on enemy's end.
						//EntityStat hitTimeScale = hittable.Entity.GetStat(EntityStatIdentifier.TIMESCALE);
						//if (hitTimeScale != null)
						//{
						//	hitTimeScale.AddModifier(this, timeMod);
						//}
					}
				}
			}

			if (successfulHit)
			{
				entityTimeScale.RemoveModifier(this);
				entityTimeScale.AddModifier(this, timeMod);
			}
		}
	}
}
