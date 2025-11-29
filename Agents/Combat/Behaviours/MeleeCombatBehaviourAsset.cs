using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour for melee <see cref="IMeleeCombatMove"/>s that manages hit-detection during performance.
	/// </summary>
	[CreateAssetMenu(fileName = "CombatBehaviour_MeleeAttack", menuName = "ScriptableObjects/Combat/MeleeCombatBehaviourAsset")]
	public class MeleeCombatBehaviourAsset : BaseCombatMoveBehaviourAsset
	{
		[Header("Hit Detection")]
		[SerializeField] private LayerMask hitDetectionMask;
		[Header("Swinging")]
		[SerializeField, MinMaxRange(0.5f, 1.5f), Tooltip("Attack speed modifier by strength / weapon mass relation.")]
		private Vector2 strengthSpeedModRange = new Vector2(0.7f, 1.15f);
		[SerializeField] private float swingShakeMagnitude = 1f;
		[Header("Charging")]
		[SerializeField] private float chargePower = 1f;
		[Header("Storming")]
		[SerializeField] private float maxAcceleration = 20000f;
		[SerializeField] private float maxDeceleration = 2000f;
		[SerializeField] private float power = 50f;
		[SerializeField] private Vector3 stormShakeMagnitude = Vector3.one;
		[SerializeField] private float minStormDistance = 1f;

		protected IMeleeCombatMove move;
		protected CallbackService callbackService;
		protected TransformLookup transformLookup;
		protected ITargeter targeter;
		protected IAgentMovementHandler movementHandler;
		protected AgentNavigationHandler navigationHandler;
		protected IEntityCollection entityCollection;
		protected CombatSettings combatSettings;
		protected RigidbodyWrapper rigidbodyWrapper;
		protected ICommunicationChannel comms;
		protected IStunHandler stunHandler;
		protected AgentStatHandler statHandler;
		protected AgentImpactHandler agentSenseComponent;

		private EntityStat timescaleStat;
		private EntityStat limbMassStat;
		private EntityStat strengthStat;
		private EntityStat powerStat;
		private EntityStat limbOffenceStat;
		private EntityStat chargeStat;
		private EntityStat chargeSpeedStat;
		private EntityStat performSpeedStat;
		private EntityStat stormSpeedStat;
		private EntityStat enduranceCostStat;

		private FloatFuncModifier speedMod;
		private FloatOperationModifier enduranceCostMod;

		private CombatHitDetector hitDetector;
		private TimerClass inertiaTimer;
		private TimedCurveModifier hitPauseMod;
		private float totalCharge;
		private float attackRange;
		private ITargetable target;
		private bool hasStorm;
		private float stormSpeed;
		private float stormDistance;
		private float stormDuration;
		private TimerClass stormTimer;
		private ContinuousShakeSource swingShake;
		private ContinuousShakeSource stormShake;

		public void InjectDependencies(IMeleeCombatMove move, CallbackService callbackService,
			TransformLookup transformLookup, ITargeter targeter, IAgentMovementHandler movementHandler,
			AgentNavigationHandler navigationHandler, IEntityCollection entityCollection, CombatSettings combatSettings,
			RigidbodyWrapper rigidbodyWrapper, ICommunicationChannel comms, IStunHandler stunHandler,
			AgentStatHandler statHandler, AgentImpactHandler agentSenseComponent)
		{
			this.move = move;
			this.callbackService = callbackService;
			this.transformLookup = transformLookup;
			this.targeter = targeter;
			this.movementHandler = movementHandler;
			this.navigationHandler = navigationHandler;
			this.entityCollection = entityCollection;
			this.combatSettings = combatSettings;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.comms = comms;
			this.stunHandler = stunHandler;
			this.statHandler = statHandler;
			this.agentSenseComponent = agentSenseComponent;

			timescaleStat = Agent.Stats.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);
			limbMassStat = Agent.Stats.GetStat(AgentStatIdentifiers.MASS.SubStat(this.move.Limb));
			strengthStat = Agent.Stats.GetStat(AgentStatIdentifiers.STRENGTH);
			powerStat = Agent.Stats.GetStat(AgentStatIdentifiers.POWER);
			limbOffenceStat = Agent.Stats.GetStat(AgentStatIdentifiers.OFFENCE.SubStat(this.move.Limb));
			chargeStat = Agent.Stats.GetStat(move.ChargeCost.Stat);
			chargeSpeedStat = Agent.Stats.GetStat(move.ChargeSpeedMultiplierStat, false, 1f);
			performSpeedStat = Agent.Stats.GetStat(move.PerformSpeedMultiplierStat, false, 1f);
			stormSpeedStat = Agent.Stats.GetStat(AgentStatIdentifiers.ATTACK_STORM_SPEED, false, 1f);
			enduranceCostStat = Agent.Stats.GetStat(AgentStatIdentifiers.ENDURANCE.SubStat(AgentStatIdentifiers.SUB_COST));

			attackRange = this.move.Range +
				Agent.Stats.GetStat(AgentStatIdentifiers.REACH) +
				(Agent.Stats.GetStat(AgentStatIdentifiers.REACH.SubStat(this.move.Limb)) ?? 0f);
		}

		public override void Start()
		{
			base.Start();

			hitDetector = new CombatHitDetector(Agent, transformLookup, move, hitDetectionMask);
			Performer.StartedPerformingEvent += OnStartedPerformingEvent;
			totalCharge = 1f;

			speedMod = new FloatFuncModifier(ModMethod.Absolute, (float f) => f * (strengthStat / limbMassStat).Clamp(strengthSpeedModRange.x, strengthSpeedModRange.y));
			chargeSpeedStat.AddModifier(this, speedMod);
			performSpeedStat.AddModifier(this, speedMod);
			enduranceCostMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			enduranceCostStat.AddModifier(this, enduranceCostMod);
		}

		public override void Stop()
		{
			base.Stop();

			hitDetector.Dispose();
			Performer.StartedPerformingEvent -= OnStartedPerformingEvent;

			chargeSpeedStat.RemoveModifier(this);
			performSpeedStat.RemoveModifier(this);
			speedMod.Dispose();
			enduranceCostStat.RemoveModifier(this);
			enduranceCostMod.Dispose();

			movementHandler.AutoUpdateMovement = true;
			stormShake?.Dispose();
			swingShake?.Dispose();
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

			Performer.Prolong = RigidbodyWrapper.Speed > move.ProlongThreshold;

			if (Performer.State == PerformanceState.Preparing && Performer.Charge >= Move.MinCharge)
			{
				// Overcharging: Drain charge stat.
				if (Agent.Stats.TryApplyStatCost(Move.ChargeCost.Stat, move.ChargeCost.Cost * delta * chargeSpeedStat, true, out float damage, out bool drained, out float overdraw))
				{
					totalCharge += (damage - overdraw) * chargePower;
					if (drained)
					{
						// Charge stat was drained, exit charge and enter performance.
						Performer.TryPerform();
					}
				}
			}

			if (Performer.State == PerformanceState.Performing)
			{
				if (Performer.RunTime >= move.HitDetectionDelay && hitDetector.Update(out List<HitScanHitData> newHits))
				{
					OnNewHitDetected(newHits);
				}

				// Apply inertia.
				if (inertiaTimer != null && inertiaTimer.Expired)
				{
					RigidbodyWrapper.PushRelative(move.Inertia);
					inertiaTimer.Dispose();
					inertiaTimer = null;
				}
				// Apply storm.
				if (inertiaTimer == null &&
					stormTimer != null)
				{
					Vector3 dir = target != null ?
						target.Position - RigidbodyWrapper.Position :
						rigidbodyWrapper.Forward; // TODO: Steering control?
					if (stormTimer.Expired || (target != null && dir.magnitude < attackRange + target.Radius))
					{
						// If in range or out of time, end storm.
						stormTimer.Dispose();
						stormTimer = null;
						rigidbodyWrapper.TargetVelocity = Vector3.zero;
						Performer.Paused = false;
						OnSwing();
					}
					else
					{
						// Storm towards target.
						rigidbodyWrapper.TargetVelocity = dir.normalized * stormSpeed;
						rigidbodyWrapper.ApplyMovement(null, maxAcceleration, maxDeceleration, power, true);
					}
				}
				// Brake storm only if we actually used storm movement.
				if (hasStorm && inertiaTimer == null && stormTimer == null)
				{
					rigidbodyWrapper.ApplyMovement(Vector3.zero, maxAcceleration, maxDeceleration, power, true);
				}
			}

			if (Performer.State is PerformanceState.Finishing)
			{
				movementHandler.AutoUpdateMovement = true;
			}

			// Shaking.
			if (swingShake != null)
			{
				swingShake.Intensity = (Performer.RunTime / Move.MinDuration).InvertClamped();
			}
			if (stormShake != null)
			{
				stormShake.Direction = -rigidbodyWrapper.Velocity.normalized;
				stormShake.Frequency = 10f + 10f * rigidbodyWrapper.Acceleration.magnitude;
				stormShake.Intensity = rigidbodyWrapper.Speed / stormSpeed;
			}

			float balance = Performer.State == PerformanceState.Preparing ? move.ChargeBalance : move.PerformBalance;
			enduranceCostMod.SetValue((1f / balance).Lerp(1f, Weight.Invert()));
		}

		protected void OnStartedPerformingEvent(IPerformer performer)
		{
			// TARGETING
			if (targeter.Target != null)
			{
				// Aim to selected target.
				target = targeter.Target;
			}
			else if (RigidbodyWrapper.TargetVelocity.magnitude <= 1f &&
				navigationHandler.TryGetClosestTarget(Agent.Targeter.Enemies.Components, false, out ITargetable closest, out float distance) &&
				distance < attackRange + closest.Radius)
			{
				// Auto aim to closest targetable in range.
				target = closest;
			}

			if (target != null)
			{
				rigidbodyWrapper.TargetVelocity = (target.Position - RigidbodyWrapper.Position).normalized;
			}
			movementHandler.ForceRotation();

			// CHARGING
			float extraCharge = Mathf.Max(0f, totalCharge - 1f);
			float effectiveStormDistance = extraCharge * move.StormDistance;

			// Only use storm if it actually wants to travel a meaningful distance.
			hasStorm = extraCharge > 0f && effectiveStormDistance >= minStormDistance;

			if (hasStorm)
			{
				// Initialize charge > storm.
				movementHandler.AutoUpdateMovement = false; // Don't auto brake during storm.

				stormSpeed = totalCharge * (stormSpeedStat ?? 1f);
				// We already computed effectiveStormDistance above.
				stormDuration = effectiveStormDistance / stormSpeed;
				stormTimer = new TimerClass(stormDuration, () => timescaleStat, callbackService);

				if (move.PrelongCharge)
				{
					// Hold charge pose.
					performer.Paused = true;
				}

				if (Agent.Identification.HasAll(EntityLabels.PLAYER))
				{
					// Shake screen during storm.
					stormShake = new ContinuousShakeSource(stormShakeMagnitude, -rigidbodyWrapper.TargetVelocity);
					agentSenseComponent.ReportImpact(new ImpactData()
					{
						Source = Agent,
						Direction = -rigidbodyWrapper.TargetVelocity,
						Location = Agent.Transform.position,
						ShakeSource = stormShake
					});
				}
			}
			else
			{
				// Tiny overcharge or no storm distance: behave like a regular attack.
				// totalCharge still buffs damage/offsense later, but movement uses inertia.
				OnSwing();
				inertiaTimer = new TimerClass(move.InertiaDelay, () => timescaleStat, callbackService);
			}

			// STAT COST
			Agent.Stats.TryApplyStatCost(Move.PerformCost.Stat, Move.PerformCost.Cost * (limbMassStat / strengthStat) * 100f, false);
		}

		private void OnSwing()
		{
			if (Agent.Identification.HasAll(EntityLabels.PLAYER))
			{
				swingShake = new ContinuousShakeSource(new Vector3(0f, 0f, swingShakeMagnitude), rigidbodyWrapper.TargetVelocity);
				agentSenseComponent.ReportImpact(new ImpactData()
				{
					Source = Agent,
					Location = Agent.Transform.position,
					Force = 10f,
					ShakeSource = swingShake
				});
			}
		}

		protected void OnNewHitDetected(List<HitScanHitData> newHits)
		{
			foreach (HitScanHitData hit in newHits)
			{
				if (hit.GameObject.TryGetComponentRelative(out IHittable hittable))
				{
					// Generate hit data.
					Vector3 lookDir = (hittable.Entity.Transform.position - Agent.Transform.position).FlattenY().normalized;
					Vector3 inertia = move.Inertia.Look(move.Inertia);
					Vector3 direction = move.CustomDirection ? move.HitDirection.Look(lookDir) : hit.Direction;
					float power = powerStat * move.Power * totalCharge;
					float offence = limbOffenceStat * move.Offence * totalCharge;

					HitData hitData = new HitData(
						hittable,
						Agent,
						rigidbodyWrapper.Mass,
						inertia,
						hit.Point,
						direction,
						limbMassStat,
						power,
						offence
					);

					ProcessHit(hittable, hitData);
				}
			}
		}

		protected virtual void ProcessHit(IHittable hittable, HitData hitData)
		{
			// Send hit and apply return data.
			if (hittable.Hit(hitData))
			{
				float force = hitData.Data.GetValue<float>(HitDataIdentifiers.FORCE);

				// Brake to half velocity on hit.
				rigidbodyWrapper.AddForce(-rigidbodyWrapper.Velocity * 0.5f, ForceMode.VelocityChange);

				if (chargeStat != null)
				{
					chargeStat.BaseValue += force * 0.001f;
				}

				if (hitData.Data.GetValue<bool>(HitDataIdentifiers.BLOCKED))
				{
					Performer.TryCancel(true);
					rigidbodyWrapper.ResetVelocity();
					stunHandler.EnterStun(hitData, combatSettings.BlockedStunTime);
				}
				else if (hitData.Data.GetValue<bool>(HitDataIdentifiers.PARRIED))
				{
					Performer.TryCancel(true);
					rigidbodyWrapper.ResetVelocity();
					statHandler.PointStats.W.Current.BaseValue = 0f; // Drain endurance.
					stunHandler.EnterStun(hitData, combatSettings.ParriedStunTime);
				}
				else if (hitData.Data.GetValue<bool>(HitDataIdentifiers.DEFLECTED))
				{
					Performer.TryCancel(true);
					rigidbodyWrapper.ResetVelocity();
					statHandler.PointStats.W.Current.BaseValue = 0f; // Drain endurance.
					rigidbodyWrapper.Push(-hitData.Direction * force, 1f); // Share force.
					stunHandler.EnterStun(hitData, combatSettings.DeflectedStunTime);
				}

				float hitPause = combatSettings.HitPauseReceiver.Lerp(hitData.Data.GetValue<float>(HitDataIdentifiers.IMPACT) * (1f / performSpeedStat.Value));
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

				comms.Send(hitData);
				agentSenseComponent.ReportImpact(new ImpactData()
				{
					Source = Agent,
					Victim = hitData.Receiver.Entity,
					HitObject = hitData.Receiver.Entity.GameObject,
					Location = hitData.Point,
					Force = hitData.Data.GetValue(HitDataIdentifiers.FORCE, 10f),
					Direction = hitData.Direction
				});
			}
		}
	}
}
