using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour for melee <see cref="ICombatMove"/>s that manages hit-detection during performance.
	/// </summary>
	[CreateAssetMenu(fileName = "Behaviour_MeleeAttack", menuName = "ScriptableObjects/MeleeAttackBehaviourAsset")]
	public class MeleeAttackBehaviourAsset : BehaviourAsset, IUpdatable
	{
		[SerializeField] private float controlWeightSmoothing = 6f;
		[SerializeField, ConstDropdown(typeof(IIdentificationLabels))] private string[] targetLabels;
		[SerializeField] private float autoAimRange = 2f;
		[SerializeField] private LayerMask hitDetectionMask;
		[SerializeField] private float hitPause = 0.2f;
		[SerializeField] private AnimationCurve hitPauseCurve;

		private IMovePerformer performer;
		private ICombatMove move;
		private IAgent agent;
		private RigidbodyWrapper rigidbodyWrapper;
		private CallbackService callbackService;
		private TransformLookup transformLookup;
		private ITargeter targeter;
		private IAgentMovementHandler movementHandler;
		private AgentNavigationHandler navigationHandler;
		private AgentArmsComponent arms;
		private IEntityCollection entityCollection;

		private CombatHitDetector hitDetector;
		private EntityStat entityTimeScale;
		private TimedCurveModifier timeMod;
		private FloatOperationModifier controlMod;
		private bool wasPerforming;
		private Timer momentumTimer;
		private bool appliedMomentum;
		private EntityComponentFilter<ITargetable> targetables;
		private float weight;

		public void InjectDependencies(IMovePerformer performer, ICombatMove move,
			IAgent agent, RigidbodyWrapper rigidbodyWrapper, CallbackService callbackService,
			TransformLookup transformLookup, ITargeter targeter, IAgentMovementHandler movementHandler,
			AgentNavigationHandler navigationHandler, AgentArmsComponent arms, IEntityCollection entityCollection)
		{
			this.performer = performer;
			this.move = move;
			this.agent = agent;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.callbackService = callbackService;
			this.transformLookup = transformLookup;
			this.targeter = targeter;
			this.movementHandler = movementHandler;
			this.navigationHandler = navigationHandler;
			this.arms = arms;
			this.entityCollection = entityCollection;

			entityTimeScale = agent.GetStat(EntityStatIdentifier.TIMESCALE, true, 1f);
		}

		public override void Start()
		{
			base.Start();

			hitDetector = new CombatHitDetector(agent, transformLookup, move, hitDetectionMask);
			controlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			rigidbodyWrapper.Control.AddModifier(this, controlMod);
			arms.Weight.AddModifier(this, controlMod);

			targetables = new EntityComponentFilter<ITargetable>(entityCollection, (entity) => entity.Identification.HasAll(targetLabels), (c) => true, agent);

			performer.PoseUpdateEvent += OnPoseUpdateEvent;
		}

		public override void Stop()
		{
			base.Stop();

			rigidbodyWrapper.Control.RemoveModifier(this);
			arms.Weight.RemoveModifier(this);

			performer.PoseUpdateEvent -= OnPoseUpdateEvent;

			hitDetector.Dispose();
			controlMod.Dispose();
			targetables.Dispose();
		}

		public void ExUpdate(float delta)
		{
			if (performer.State == PerformanceState.Performing)
			{
				if (performer.RunTime >= move.HitDetectionDelay && hitDetector.Update(out List<HitScanHitData> newHits))
				{
					OnNewHitDetected(newHits);
				}

				if (!wasPerforming)
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
					if (move.PerformCost.Count > 0)
					{
						// Apply performance costs to stats.
						foreach (StatCost statCost in move.PerformCost)
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
					momentumTimer = new Timer(move.ForceDelay);
					appliedMomentum = false;
				}
				wasPerforming = true;

				// Apply momentum to user after delay.
				if (!appliedMomentum && !momentumTimer)
				{
					rigidbodyWrapper.AddImpactRelative(move.Inertia);
					appliedMomentum = true;
				}
			}

			// Set control.
			float control = 1f - weight;
			controlMod.SetValue(controlMod.Value < control ? Mathf.Lerp(controlMod.Value, control, controlWeightSmoothing * Time.deltaTime) : control);
		}

		private void OnPoseUpdateEvent(IPerformer performer, PoserStruct pose, float weight)
		{
			this.weight = weight;
		}

		private void OnNewHitDetected(List<HitScanHitData> newHits)
		{
			// TODO: Have hit pause duration depend on penetration % (factoring in power, sharpness, hardness.)
			timeMod = new TimedCurveModifier(ModMethod.Absolute, hitPauseCurve, new Timer(hitPause), callbackService);

			bool successfulHit = false;
			foreach (HitScanHitData hit in newHits)
			{
				if (hit.GameObject.TryGetComponentRelative(out IHittable hittable))
				{
					Vector3 inertia = move.Inertia.Look((hittable.Entity.Transform.position - agent.Transform.position).FlattenY());

					// Calculate attack force.
					float strength = 0f;
					if (agent.TryGetStat(move.StrengthStat, out EntityStat strengthStat))
					{
						strength = strengthStat;
					}

					float force = rigidbodyWrapper.Mass + strength;

					// Generate hit-data for hittable.
					HitData hitData = new HitData(
						agent,
						hittable,
						inertia,
						force,
						hit.Direction,
						false,
						new Dictionary<string, float>()
					);

					// If move is offensive, add base health damage to HitData.
					if (move.Offensive &&
						agent.TryGetStat(move.OffenceStat, out EntityStat offence) &&
						hittable.Entity.TryGetStat(AgentStatIdentifiers.DEFENCE, out EntityStat defence))
					{
						float damage = SpaxFormulas.GetDamage(offence, defence) * move.Offensiveness;
						hitData.Damages.Add(AgentStatIdentifiers.HEALTH, damage);
					}

					// Invoke hit event to allow adding of additional damage.
					//ProcessHitEvent?.Invoke(hitData);

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
