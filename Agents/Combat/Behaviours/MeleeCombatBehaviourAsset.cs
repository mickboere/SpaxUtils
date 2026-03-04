using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour for melee IMeleeCombatMove that manages hit-detection during performance.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(MeleeCombatBehaviourAsset), menuName = "Performance/Behaviour/" + nameof(MeleeCombatBehaviourAsset))]
	public class MeleeCombatBehaviourAsset : BaseCombatMoveBehaviourAsset
	{
		[Header("Hit Detection")]
		[SerializeField] private LayerMask hitDetectionMask;

		[Header("Swinging")]
		[SerializeField, MinMaxRange(0.5f, 1.5f), Tooltip("Attack speed modifier by strength / weapon mass relation.")]
		private Vector2 strengthSpeedModRange = new Vector2(0.7f, 1.15f);
		[SerializeField, Range(0f, 1f), Tooltip("Minimum power factor when heavily under-strengthed.")]
		private float minPowerFactor = 0.4f;
		[SerializeField, Range(0f, 1f), Tooltip("Minimum swing speed factor at the start of a very heavy swing.")]
		private float minInertiaSpeedFactor = 0.4f;
		[SerializeField] private float swingShakeMagnitude = 1f;

		[Header("Charging")]
		[SerializeField] float chargeConversionRatio = 0.005f; // 100 Points drained = +0.5x Multiplier (50% boost)
		[SerializeField] float maxChargeMultiplier = 3.0f;     // Hard cap at 3x charge (prevent absurd forces)
		[SerializeField, Tooltip("How much extra crit chance rating per unit of extraCharge")]
		private float chargeCritBonusFactor = 1f;
		[SerializeField, Range(0f, 1f)] float chargeDamageEfficiency = 0.25f;

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
		private EntityStat piercingStat;
		private EntityStat powerStat;
		private EntityStat precisionStat;
		private EntityStat luckStat;
		private EntityStat chargeStat;
		private EntityStat chargeSpeedStat;
		private EntityStat performSpeedStat;
		private EntityStat stormSpeedStat;
		private EntityStat enduranceCostStat;

		private FloatFuncModifier speedMod;
		private FloatOperationModifier swingPhaseSpeedMod;
		private FloatOperationModifier enduranceCostMod;

		private CombatHitDetector hitDetector;
		private TimerClass inertiaTimer;
		private TimedCurveModifier hitPauseMod;
		private float totalCharge;
		private float accumulatedChargePoints; // New: Tracks raw drain for precision
		private float attackRange;
		private ITargetable target;
		private bool hasStorm;
		private float stormSpeed;
		private float stormDuration;
		private TimerClass stormTimer;
		private ContinuousShakeSource swingShake;
		private ContinuousShakeSource stormShake;

		// Strength/mass derived per-swing values.
		private float wieldRatio = 1f;
		private float baseStrengthSpeedFactor = 1f;
		private float baseStrengthPowerFactor = 1f;

		public void InjectDependencies(
			IMeleeCombatMove move,
			CallbackService callbackService,
			TransformLookup transformLookup,
			ITargeter targeter,
			IAgentMovementHandler movementHandler,
			AgentNavigationHandler navigationHandler,
			IEntityCollection entityCollection,
			CombatSettings combatSettings,
			RigidbodyWrapper rigidbodyWrapper,
			ICommunicationChannel comms,
			IStunHandler stunHandler,
			AgentStatHandler statHandler,
			AgentImpactHandler agentSenseComponent)
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
			piercingStat = Agent.Stats.GetStat(AgentStatIdentifiers.PIERCING);
			powerStat = Agent.Stats.GetStat(AgentStatIdentifiers.POWER);
			precisionStat = Agent.Stats.GetStat(AgentStatIdentifiers.PRECISION);
			luckStat = Agent.Stats.GetStat(AgentStatIdentifiers.LUCK, true);
			chargeStat = Agent.Stats.GetStat(move.ChargeCost.Stat);
			chargeSpeedStat = Agent.Stats.GetStat(move.ChargeSpeedMultiplierStat, false);
			performSpeedStat = Agent.Stats.GetStat(move.PerformSpeedMultiplierStat, false);
			stormSpeedStat = Agent.Stats.GetStat(AgentStatIdentifiers.STORM_SPEED, false);
			enduranceCostStat = Agent.Stats.GetStat(AgentStatIdentifiers.ENDURANCE.SubStat(AgentStatIdentifiers.SUB_DRAIN));

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
			accumulatedChargePoints = 0f;

			// Compute wield ratio and base factors once per behaviour instance.
			float mass = limbMassStat;
			float strength = strengthStat;
			wieldRatio = mass > 0f ? strength / mass : 1f;
			if (wieldRatio < 0f) wieldRatio = 0f;

			baseStrengthSpeedFactor = ComputeBaseStrengthSpeedFactor(wieldRatio);
			baseStrengthPowerFactor = ComputeBaseStrengthPowerFactor(wieldRatio);

			// Base strength-speed modifier (constant over the swing).
			speedMod = new FloatFuncModifier(
				ModMethod.Absolute,
				f => f * baseStrengthSpeedFactor
			);
			chargeSpeedStat?.AddModifier(this, speedMod);
			performSpeedStat?.AddModifier(this, speedMod);

			// Phase-based inertia modifier (varies during swing).
			swingPhaseSpeedMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			performSpeedStat?.AddModifier(this, swingPhaseSpeedMod);

			enduranceCostMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			enduranceCostStat?.AddModifier(this, enduranceCostMod);
		}

		public override void Stop()
		{
			base.Stop();

			hitDetector.Dispose();
			Performer.StartedPerformingEvent -= OnStartedPerformingEvent;

			chargeSpeedStat?.RemoveModifier(this);
			performSpeedStat?.RemoveModifier(this);
			speedMod.Dispose();
			swingPhaseSpeedMod?.Dispose();

			enduranceCostStat?.RemoveModifier(this);
			enduranceCostMod.Dispose();

			movementHandler.AutoUpdateMovement = true;
			stormShake?.Dispose();
			swingShake?.Dispose();
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

			Performer.Prolong = RigidbodyWrapper.Speed > move.ProlongThreshold;

			if (Performer.State == PerformanceState.Preparing && Performer.ChargeTime >= Move.MinCharge)
			{
				// 1. Calculate Drain Rate based on PRECISION (PhysicStat)
				float actualDrain = precisionStat * delta * (chargeSpeedStat != null ? chargeSpeedStat.Value : 1f);

				// 2. Drain the Static (PointStat)
				float damage = statHandler.PointStats.NE.Drain(actualDrain, out bool drained);

				// 3. Store raw drain for Precision calc (Uncapped)
				accumulatedChargePoints += damage;

				// 4. Calculate Power Multiplier (Clamped)
				float rawMultiplier = 1f + (accumulatedChargePoints * chargeConversionRatio);
				totalCharge = Mathf.Min(rawMultiplier, maxChargeMultiplier);

				if (drained)
				{
					// Pool empty, force release
					Performer.TryPerform();
				}
			}

			if (Performer.State == PerformanceState.Performing)
			{
				// Phase-based inertia: heavy swings start slow, then catch up.
				float phase = Mathf.Clamp01(Performer.RunTime / Move.MinDuration).InSine();
				float phaseMult = GetPhaseInertiaMultiplier(phase);
				if (swingPhaseSpeedMod != null)
				{
					swingPhaseSpeedMod.SetValue(phaseMult);
				}

				if (Performer.RunTime >= move.HitDetectionDelay &&
					hitDetector.Update(out List<HitScanHitData> newHits))
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
				if (inertiaTimer == null && stormTimer != null)
				{
					Vector3 dir = target != null
						? target.Position - RigidbodyWrapper.Position
						: rigidbodyWrapper.Forward;

					if (stormTimer.Expired ||
						(target != null && dir.magnitude < attackRange + target.Radius))
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
			else
			{
				if (swingPhaseSpeedMod != null)
				{
					swingPhaseSpeedMod.SetValue(1f);
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

			float balance = Performer.State == PerformanceState.Preparing
				? move.ChargeBalance
				: move.PerformBalance;

			enduranceCostMod.SetValue((1f / balance).Lerp(1f, Weight.Invert()));
		}

		/// <summary>
		/// Computes base speed factor from strength/mass ratio.
		/// </summary>
		private float ComputeBaseStrengthSpeedFactor(float ratio)
		{
			if (ratio <= 1f)
			{
				return Mathf.Lerp(strengthSpeedModRange.x, 1f, Mathf.Clamp01(ratio));
			}
			else
			{
				float extra = Mathf.Clamp01(ratio - 1f);
				return Mathf.Lerp(1f, strengthSpeedModRange.y, extra);
			}
		}

		/// <summary>
		/// Computes base power factor from strength/mass ratio.
		/// </summary>
		private float ComputeBaseStrengthPowerFactor(float ratio)
		{
			if (ratio >= 1f)
			{
				return 1f;
			}
			return Mathf.Lerp(minPowerFactor, 1f, Mathf.Clamp01(ratio));
		}

		/// <summary>
		/// Returns a phase-based multiplier for swing speed/power:
		/// </summary>
		private float GetPhaseInertiaMultiplier(float phase)
		{
			float clampedRatio = Mathf.Clamp01(wieldRatio);
			float heaviness = 1f - clampedRatio;
			float earlySlow = Mathf.Lerp(1f, minInertiaSpeedFactor, heaviness);

			return Mathf.Lerp(earlySlow, 1f, Mathf.Clamp01(phase));
		}

		protected void OnStartedPerformingEvent(IPerformer performer)
		{
			// TARGETING logic ...
			if (targeter.Target != null)
			{
				target = targeter.Target;
			}
			else if (RigidbodyWrapper.TargetVelocity.magnitude <= 1f &&
					 navigationHandler.TryGetClosestTarget(
						 Agent.Targeter.Enemies.Components,
						 false,
						 out ITargetable closest,
						 out float distance) &&
					 distance < attackRange + closest.Radius)
			{
				target = closest;
			}

			if (target != null)
			{
				rigidbodyWrapper.TargetVelocity = (target.Position - RigidbodyWrapper.Position).normalized;
			}
			movementHandler.ForceRotation(null, Agent.Mind.Personality.E.OutQuad());

			// CHARGING
			float extraCharge = Mathf.Max(0f, totalCharge - 1f);
			float effectiveStormDistance = extraCharge * move.StormDistance;

			hasStorm = extraCharge > 0f && effectiveStormDistance >= minStormDistance;

			if (hasStorm)
			{
				movementHandler.AutoUpdateMovement = false;

				stormSpeed = totalCharge * (stormSpeedStat ?? 1f);
				stormDuration = effectiveStormDistance / stormSpeed;
				stormTimer = new TimerClass(stormDuration, () => timescaleStat, callbackService);

				if (move.PrelongCharge)
				{
					performer.Paused = true;
				}

				if (Agent.Identification.HasAll(EntityLabels.PLAYER))
				{
					stormShake = new ContinuousShakeSource(stormShakeMagnitude, -rigidbodyWrapper.TargetVelocity);
					agentSenseComponent.ReportImpact(new ImpactData
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
				OnSwing();
				inertiaTimer = new TimerClass(move.InertiaDelay, () => timescaleStat, callbackService);
			}

			statHandler.PointStats.N.Drain(Move.PerformCost.Cost * (limbMassStat / strengthStat) * 100f);
		}

		private void OnSwing()
		{
			if (Agent.Identification.HasAll(EntityLabels.PLAYER))
			{
				swingShake = new ContinuousShakeSource(
					new Vector3(0f, 0f, swingShakeMagnitude),
					rigidbodyWrapper.TargetVelocity);

				agentSenseComponent.ReportImpact(new ImpactData
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
					Vector3 lookDir = (hittable.Entity.Transform.position - Agent.Transform.position)
						.FlattenY().normalized;
					Vector3 inertia = move.Inertia.Look(move.Inertia);
					Vector3 direction = move.CustomDirection ? move.HitDirection.Look(lookDir) : hit.Direction;

					float mass = limbMassStat;
					float phase = Mathf.Clamp01(Performer.RunTime / Move.MinDuration);
					float phaseMult = GetPhaseInertiaMultiplier(phase);

					float basePower = powerStat * move.Power * baseStrengthPowerFactor;
					float powerValue = basePower * totalCharge * phaseMult;

					// --- MALICE LOGIC ---
					float basePierce = piercingStat * move.Piercing;
					float maliceBonus = 0f;

					if (basePierce > 0f)
					{
						// Attempt to drain Malice equal to the Pierce of the attack (The conduit capacity).
						// Damage() returns the Cost (base * multiplier), handling overdraw if pool is low.
						float drained = statHandler.PointStats.NW.Drain(basePierce, true);

						// Calculate coverage ratio. If we paid 100% of the cost, we get 100% bonus.
						float coverage = drained / basePierce;

						// The Malice added is equal to the BASE PIERCE * Coverage. 
						// (Symmetrical to Grace: You get out what you put in, scaled by resource availability).
						maliceBonus = basePierce * coverage;
					}

					// Final Pierce = Base + Malice.
					float finalPierce = basePierce + maliceBonus;

					// Final precision = Base + Charge.
					float finalPrecision = precisionStat * move.Precision + (accumulatedChargePoints * chargeDamageEfficiency);

					HitData hitData = new HitData(
						hittable,
						Agent,
						rigidbodyWrapper.Mass,
						inertia,
						hit.Point,
						direction,
						mass,
						finalPierce,
						powerValue,
						finalPrecision,
						luckStat
					);

					ProcessHit(hittable, hitData);
				}
			}
		}

		protected virtual void ProcessHit(IHittable hittable, HitData hitData)
		{
			if (hittable.Hit(hitData))
			{
				// This statement is entered after the target has fully processed the hit,
				// meaning all return data is present in the hitData.

				float force = hitData.Data.GetValue<float>(HitDataIdentifiers.FORCE);

				rigidbodyWrapper.AddForce(
					-rigidbodyWrapper.Velocity * 0.5f,
					ForceMode.VelocityChange);

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
					statHandler.PointStats.W.Current.BaseValue = 0f;
					stunHandler.EnterStun(hitData, combatSettings.ParriedStunTime);
				}
				else if (hitData.Data.GetValue<bool>(HitDataIdentifiers.DEFLECTED))
				{
					Performer.TryCancel(true);
					rigidbodyWrapper.ResetVelocity();
					statHandler.PointStats.W.Current.BaseValue = 0f;
					rigidbodyWrapper.Push(-hitData.Direction * force, 1f);
					stunHandler.EnterStun(hitData, combatSettings.DeflectedStunTime);
				}

				float impact = hitData.Data.GetValue<float>(HitDataIdentifiers.IMPACT);
				float hitPause = combatSettings.HitPauseReceiver.Lerp(
					impact * (1f / performSpeedStat.Value));

				if (hitPauseMod == null || hitPause > hitPauseMod.Timer.Remaining)
				{
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
				agentSenseComponent.ReportImpact(new ImpactData
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
