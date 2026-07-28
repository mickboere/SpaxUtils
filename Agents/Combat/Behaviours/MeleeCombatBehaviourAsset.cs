using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour for melee IMeleeCombatMove that manages hit-detection during performance.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(MeleeCombatBehaviourAsset), menuName = "Performance/Behaviour/" + nameof(MeleeCombatBehaviourAsset))]
	public class MeleeCombatBehaviourAsset : BaseCombatMoveBehaviourAsset, IChargeProvider
	{
		/// <inheritdoc/>
		public float ChargeMultiplier => totalCharge;

		[Header("Hit Detection")]
		[SerializeField] private LayerMask hitDetectionMask;

		[Header("Swinging")]
		// Wield speed curve (strengthSpeedModRange / speedCurveExponent / overStrengthFullRatio) lifted to CombatSettings
		// — it's a universal mechanic and the AI's move-selection + strike-timing need to read it before the swing.
		[SerializeField, Tooltip("Reference mass at which swing cost equals Move.PerformCost.Cost x 100.")]
		private float massNormalizer = 2f;
		[SerializeField, Range(0f, 1f), Tooltip("Minimum power factor when heavily under-strengthed.")]
		private float minPowerFactor = 0.4f;
		[SerializeField, Range(0f, 1f), Tooltip("Minimum swing speed factor at the start of a very heavy swing.")]
		private float minInertiaSpeedFactor = 0.4f;
		[SerializeField] private float swingShakeMagnitude = 1.5f;

		[Header("Charging")]
		// chargeConversionRatio + maxChargeMultiplier moved to CombatSettings (global Static→charge economy).
		[SerializeField, Tooltip("How much extra crit chance rating per unit of extraCharge")]
		private float chargeCritBonusFactor = 1f;
		[SerializeField, Range(0f, 1f)] float chargeDamageEfficiency = 0.25f;

		[Header("Storming")]
		[SerializeField] private float maxAcceleration = 20000f;
		[SerializeField] private float maxDeceleration = 2000f;
		[SerializeField] private float power = 50f;
		[SerializeField] private Vector3 stormShakeMagnitude = Vector3.one * 2;
		[SerializeField] private float minStormDistance = 1f;

		protected IMeleeCombatMove move;
		protected CallbackService callbackService;
		protected TransformLookup transformLookup;
		protected ITargeter targeter;
		protected ITargetable agentTargetable;
		protected IAgentMovementHandler movementHandler;
		protected AgentNavigationHandler navigationHandler;
		protected IEntityCollection entityCollection;
		protected CombatSettings combatSettings;
		protected RigidbodyWrapper rigidbodyWrapper;
		protected ICommunicationChannel comms;
		protected IStunHandler stunHandler;
		protected AgentStatHandler statHandler;
		protected AgentCombatComponent combatComponent;
		protected AgentImpactHandler agentImpactHandler;
		protected AgentAudioHandler agentAudioHandler;

		private EntityStat timescaleStat;
		private EntityStat limbMassStat;
		private EntityStat strengthStat;

		/// <summary>
		/// Mass behind the striking limb. When the move has no limb (kicks, body rams) there is no dedicated MASS
		/// substat, so the strike mass falls back to a fraction of the agent's whole-body mass
		/// (<see cref="IMeleeCombatMove.NaturalStrikeMassFraction"/>) - never the full body weight, and never a
		/// null dereference.
		/// </summary>
		private float LimbMass => limbMassStat != null
			? (float)limbMassStat
			: rigidbodyWrapper.Mass * move.NaturalStrikeMassFraction;
		private EntityStat pierceStat;
		private EntityStat luckStat;
		private EntityStat chargeStat;
		private EntityStat chargeSpeedStat;
		private EntityStat performSpeedStat;
		private EntityStat stormSpeedStat;
		private EntityStat enduranceCostStat;
		private EntityStat poiseStat;

		private FloatFuncModifier speedMod;
		private FloatOperationModifier swingPhaseSpeedMod;
		private FloatOperationModifier enduranceCostMod;

		private CombatHitDetector hitDetector;
		private bool wasScanning;
		private float lastRunTime;
		private TimerClass inertiaTimer;
		private TimedCurveModifier hitPauseMod;
		private float totalCharge;
		private float accumulatedChargePoints; // New: Tracks raw drain for pierce
		private bool chargeRewarded; // Whether this swing's charge has already paid Light EXP.
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
			ITargetable targetable,
			IAgentMovementHandler movementHandler,
			AgentNavigationHandler navigationHandler,
			IEntityCollection entityCollection,
			CombatSettings combatSettings,
			RigidbodyWrapper rigidbodyWrapper,
			ICommunicationChannel comms,
			IStunHandler stunHandler,
			AgentStatHandler statHandler,
			AgentCombatComponent combatComponent,
			AgentImpactHandler agentImpactHandler,
			AgentAudioHandler agentAudioHandler)
		{
			this.move = move;
			this.callbackService = callbackService;
			this.transformLookup = transformLookup;
			this.targeter = targeter;
			this.agentTargetable = targetable;
			this.movementHandler = movementHandler;
			this.navigationHandler = navigationHandler;
			this.entityCollection = entityCollection;
			this.combatSettings = combatSettings;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.comms = comms;
			this.stunHandler = stunHandler;
			this.statHandler = statHandler;
			this.combatComponent = combatComponent;
			this.agentImpactHandler = agentImpactHandler;
			this.agentAudioHandler = agentAudioHandler;

			timescaleStat = Agent.Stats.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);
			limbMassStat = Agent.Stats.GetStat(AgentStatIdentifiers.MASS.SubStat(this.move.Limb));
			strengthStat = Agent.Stats.GetStat(AgentStatIdentifiers.STRENGTH);
			pierceStat = Agent.Stats.GetStat(AgentStatIdentifiers.PIERCE);
			luckStat = Agent.Stats.GetStat(AgentStatIdentifiers.LUCK, true);
			chargeStat = Agent.Stats.GetStat(move.ChargeCost.Stat);
			chargeSpeedStat = Agent.Stats.GetStat(move.ChargeSpeedMultiplierStat, false);
			performSpeedStat = Agent.Stats.GetStat(move.PerformSpeedMultiplierStat, false);
			stormSpeedStat = Agent.Stats.GetStat(AgentStatIdentifiers.STORM_SPEED, false);
			enduranceCostStat = Agent.Stats.GetStat(AgentStatIdentifiers.ENDURANCE.SubStat(AgentStatIdentifiers.SUB_DRAIN));
			poiseStat = Agent.Stats.GetStat(AgentStatIdentifiers.POISE, true);

			attackRange = this.move.Range +
				Agent.Stats.GetStat(AgentStatIdentifiers.REACH) +
				(Agent.Stats.GetStat(AgentStatIdentifiers.REACH.SubStat(this.move.Limb)) ?? 0f);
		}

		public override void Start()
		{
			base.Start();

			hitDetector = new CombatHitDetector(Agent, transformLookup, move, hitDetectionMask);
			Performer.StartedPerformingEvent += OnStartedPerformingEvent;

			// Hit detection runs in LateUpdate, AFTER Unity's Animator has baked this frame's swing pose into the
			// skeleton. The pose is applied via Animator parameters whose bone transforms only get written during
			// the Animator's internal pass (after every Update, before LateUpdate). The rest of this behaviour
			// ticks in the Update phase, so casting there reads bones still holding the PREVIOUS frame's bake -
			// lagging the gate/RunTime, and the visible blade, by one frame of RunTime (= dt x timescale). That
			// lag is why detection started at a wildly different swing position per timescale. Reading post-bake
			// keeps the cast synced with the rendered pose at every timescale.
			callbackService.SubscribeUpdate(UpdateMode.LateUpdate, this, LateUpdateHitDetection);

			totalCharge = 1f;
			accumulatedChargePoints = 0f;
			chargeRewarded = false;

			// Compute wield ratio and base factors once per behaviour instance.
			if (limbMassStat != null)
			{
				// Armed/limbed strike: speed & power scale with how well the agent's strength wields the
				// limb (+ any equipped weapon mass folded into the limb's MASS substat).
				float mass = (float)limbMassStat;
				float strength = strengthStat;
				wieldRatio = mass > 0f ? strength / mass : 1f;
				if (wieldRatio < 0f)
				{
					wieldRatio = 0f;
				}
			}
			else
			{
				// Natural strike (kick, body ram): no limb/weapon to wield, so the strength-vs-mass relation is
				// neutral - the move swings at its designed speed & power, independent of any equipped weapon.
				// Its hit mass is instead a fraction of body mass (see LimbMass / NaturalStrikeMassFraction).
				wieldRatio = 1f;
			}

			baseStrengthSpeedFactor = combatSettings.WieldSpeedFactor(wieldRatio);
			baseStrengthPowerFactor = ComputeBaseStrengthPowerFactor(wieldRatio);

			// Base strength-speed modifier (constant over the swing).
			speedMod = new FloatFuncModifier(
				ModMethod.Absolute,
				f => f * baseStrengthSpeedFactor
			);
			chargeSpeedStat?.AddModifier(speedMod);
			performSpeedStat?.AddModifier(speedMod);

			// Phase-based inertia modifier (varies during swing).
			swingPhaseSpeedMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			performSpeedStat?.AddModifier(swingPhaseSpeedMod);

			enduranceCostMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			enduranceCostStat?.AddModifier(enduranceCostMod);
		}

		public override void Stop()
		{
			base.Stop();

			hitDetector.Dispose();
			Performer.StartedPerformingEvent -= OnStartedPerformingEvent;
			callbackService.UnsubscribeUpdate(UpdateMode.LateUpdate, this);

			chargeSpeedStat?.RemoveModifier(speedMod);
			performSpeedStat?.RemoveModifier(speedMod);
			speedMod.Dispose();

			performSpeedStat?.RemoveModifier(swingPhaseSpeedMod);
			swingPhaseSpeedMod?.Dispose();

			enduranceCostStat?.RemoveModifier(enduranceCostMod);
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
				// 1. Calculate Drain Rate based on PIERCE (PhysicStat)
				float actualDrain = pierceStat * delta * (chargeSpeedStat != null ? chargeSpeedStat.Value : 1f);

				// 2. Drain the Static (PointStat)
				float damage = statHandler.PointStats.NE.Drain(actualDrain, out bool drained);

				// 3. Store raw drain for Pierce calc (Uncapped)
				accumulatedChargePoints += damage;

				// 4. Calculate Power Multiplier (Clamped)
				float rawMultiplier = 1f + (accumulatedChargePoints * combatSettings.ChargeConversionRatio);
				totalCharge = Mathf.Min(rawMultiplier, combatSettings.MaxChargeMultiplier);

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

			// Hit detection is ticked in LateUpdate (see LateUpdateHitDetection) so the collider sweep reads the
			// skeleton after the Animator has baked this frame's swing pose, keeping the cast synced with the
			// rendered blade at every timescale rather than lagging it by a frame of RunTime.

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

			float chargeBalance = move.OverrideBalance ? move.ChargeBalance : combatSettings.ChargeBalance;
			float performBalance = move.OverrideBalance ? move.PerformBalance : combatSettings.PerformBalance;
			float balance = Performer.State == PerformanceState.Preparing
				? chargeBalance
				: performBalance;

			// Poise divides the imbalance excess over 1 (Guard-style), clamping toward x1.
			float imbalance = 1f / balance;
			float composed = 1f + (imbalance - 1f) / poiseStat.Value;
			enduranceCostMod.SetValue(composed.Lerp(1f, Weight.Invert()));

			// Keep the lunge from closing into the target's face.
			EnforceSeparationFloor(delta);
		}

		/// <summary>
		/// Hit detection, ticked in LateUpdate so the collider sweep reads the skeleton AFTER Unity's Animator
		/// has baked this frame's swing pose. The swing pose is applied via Animator parameters whose bone
		/// transforms only get written during the Animator's internal pass (after every Update, before
		/// LateUpdate), so casting from the Update phase reads the previous frame's bake - lagging the gate by
		/// one frame of RunTime (= dt x timescale), which made detection start at a different swing position per
		/// timescale.
		/// Ticked every frame so the stored orientation stays one frame old; only actually casts for hits once
		/// into the swing past the hit-detection delay. Tracking through the wind-up is what stops the first
		/// sweep from spanning all the way back to the move's start pose.
		/// </summary>
		private void LateUpdateHitDetection(float delta)
		{
			bool detectHits = Performer.State == PerformanceState.Performing && Performer.RunTime >= move.HitDetectionDelay;
			bool firstScan = detectHits && !wasScanning;

			// On the first detection frame, begin the sweep from the interpolated pose at the EXACT delay
			// crossing between last frame and this one - not the full previous frame. The previous frame can sit
			// a big slice of the swing back at high timescale / low fps, which would make detection start earlier
			// the coarser the frame step; interpolating the crossing makes the start frame-rate independent.
			float sweepStart = 0f;
			if (firstScan)
			{
				float span = Performer.RunTime - lastRunTime;
				sweepStart = span > 0f ? Mathf.Clamp01((move.HitDetectionDelay - lastRunTime) / span) : 0f;
			}
			wasScanning = detectHits;
			lastRunTime = Performer.RunTime;

			if (hitDetector.Update(detectHits, sweepStart, out List<HitScanHitData> newHits))
			{
				OnNewHitDetected(newHits);
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

		/// <summary>
		/// Hard floor that stops the lunge from closing into the target's face. Two horizontal zones around
		/// the target: the combined top-down radii (inner) is an impenetrable wall, and a padding ring out to
		/// inner + <see cref="CombatSettings.MeleeFloorPadding"/> metres holds a critically-damped penalty
		/// spring that pushes the attacker back out. The padding is absolute, so the ring stays a fixed width
		/// regardless of target size (giant enemies). Outside the ring it's a pure no-op, so the approach is
		/// never throttled — it only engages once the lunge would breach the buffer.
		/// </summary>
		private void EnforceSeparationFloor(float delta)
		{
			if (target == null || agentTargetable == null || delta <= 0f)
			{
				return;
			}

			Vector3 toTarget = (target.Position - rigidbodyWrapper.Position).FlattenY();
			float distance = toTarget.magnitude;
			if (distance <= 0.0001f)
			{
				return;
			}

			float inner = agentTargetable.Radius + target.Radius;
			float outer = inner + combatSettings.MeleeFloorPadding;
			if (distance >= outer)
			{
				// Outside the buffer: leave the approach untouched.
				return;
			}

			Vector3 outward = -toTarget / distance;

			// Critically-damped penalty spring (acceleration form, mass-independent): rests the attacker at
			// 'outer', ramping the outward push with penetration depth so a hard lunge is bled off before 'inner'.
			float penetration = outer - distance;
			float outwardSpeed = Vector3.Dot(rigidbodyWrapper.Velocity, outward);
			float stiffness = combatSettings.MeleeFloorStiffness;
			float acceleration = stiffness * penetration - 2f * Mathf.Sqrt(stiffness) * outwardSpeed;
			if (acceleration > 0f)
			{
				rigidbodyWrapper.AddForce(outward * (acceleration * delta), ForceMode.VelocityChange);
			}

			// Impenetrable inner wall: reposition out and kill any remaining closing velocity.
			if (distance < inner)
			{
				Vector3 clamped = target.Position + outward * inner;
				rigidbodyWrapper.Position = new Vector3(clamped.x, rigidbodyWrapper.Position.y, clamped.z);

				float closing = Vector3.Dot(rigidbodyWrapper.Velocity, -outward);
				if (closing > 0f)
				{
					rigidbodyWrapper.AddForce(outward * closing, ForceMode.VelocityChange);
				}
			}
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
					agentImpactHandler.ReportImpact(new ImpactData
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

			// Play exertion audio.
			float drained = statHandler.PointStats.N.Drain(Move.PerformCost.Cost * (LimbMass / massNormalizer) * 100f);
			float fraction = drained / statHandler.PointStats.N.Reserve;
			agentAudioHandler.PlayExertion(fraction);
		}

		private void OnSwing()
		{
			if (Agent.Identification.HasAll(EntityLabels.PLAYER))
			{
				swingShake = new ContinuousShakeSource(
					new Vector3(0f, 0f, swingShakeMagnitude),
					rigidbodyWrapper.TargetVelocity);

				agentImpactHandler.ReportImpact(new ImpactData
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
				if (hit.GameObject.TryGetComponentRelative(out IHittable hittable) &&
					!targeter.Allies.Components.Any(a => a.Entity == hittable.Entity))
				{
					// Generate hit data.
					Vector3 lookDir = (hittable.Entity.Transform.position - Agent.Transform.position)
						.FlattenY().normalized;
					// Hitter's actual world-space closing velocity: shared with the receiver on contact
					// (inelastic clash) and used as the parry pushback axis. Reflects real motion/facing.
					Vector3 inertia = rigidbodyWrapper.Velocity;
					Vector3 direction = move.CustomDirection ? move.HitDirection.Look(lookDir) : hit.Direction;

					float mass = LimbMass;
					float phase = Mathf.Clamp01(Performer.RunTime / Move.MinDuration);
					float phaseMult = GetPhaseInertiaMultiplier(phase);

					// Per-axis base output (x=Slash, y=Power, z=Pierce) from the central authority
					// (AgentCombatComponent): move sliders × equipped weapon × body physics, normalised.
					// Runtime-only modifiers (strength, charge, phase, malice) are applied below.
					Vector3 baseOutput = combatComponent.GetMoveOutput(move).Output;

					float basePower = baseOutput.y * baseStrengthPowerFactor;
					float powerValue = basePower * totalCharge * phaseMult;

					// Assemble the offence vector's runtime-modified channels before Malice.
					float slashValue = baseOutput.x;
					float pierceValue = baseOutput.z + (accumulatedChargePoints * chargeDamageEfficiency);

					// --- MALICE: whole-vector amplification ---
					// Spite scales the entire offence. Conduit capacity = total output; coverage = the fraction the
					// Malice pool pays for (Drain applies the Hostility-driven DrainMult). Symmetrical to Grace.
					float totalOffence = slashValue + powerValue + pierceValue;
					float maliceMult = 1f;
					float maliceDrained = 0f;
					if (totalOffence > 0f)
					{
						maliceDrained = statHandler.PointStats.NW.Drain(totalOffence, true);
						maliceMult += maliceDrained / totalOffence; // Ceiling is 1 + DrainMult; cap it via the Hostility→Drain mapping.
					}

					float finalSlash = slashValue * maliceMult;
					float finalPower = powerValue * maliceMult;
					float finalPierce = pierceValue * maliceMult;

					HitData hitData = new HitData(
						hittable,
						Agent,
						rigidbodyWrapper.Mass,
						inertia,
						hit.Point,
						direction,
						mass,
						finalSlash,
						finalPower,
						finalPierce,
						luckStat
					);

					ProcessHit(hittable, hitData);
					RewardHitExp(hitData, maliceDrained);
				}
			}
		}

		/// <summary>
		/// Rewards the attacker for what the hit actually landed. Output is measured against the receiver's max
		/// health, so one kill amounts to a single bar split over Fire, Light and Void by how it was dealt.
		/// </summary>
		private void RewardHitExp(HitData hitData, float maliceDrained)
		{
			float healthMax = hitData.Data.GetValue<float>(HitDataIdentifiers.HEALTH_MAX);
			if (healthMax <= 0f)
			{
				return;
			}

			statHandler.RewardExp(Element.Fire,
				hitData.Data.GetValue<float>(HitDataIdentifiers.BLUNT_DAMAGE) / healthMax, ExpSources.POWER_OUTPUT);
			statHandler.RewardExp(Element.Light,
				hitData.Data.GetValue<float>(HitDataIdentifiers.CRIT_DAMAGE) / healthMax, ExpSources.PIERCE_OUTPUT);
			statHandler.RewardExp(Element.Void,
				hitData.Data.GetValue<float>(HitDataIdentifiers.SLASH_DAMAGE) / healthMax, ExpSources.SLASH_OUTPUT);

			bool neglected = hitData.Data.GetValue<bool>(HitDataIdentifiers.BLOCKED) ||
				hitData.Data.GetValue<bool>(HitDataIdentifiers.PARRIED) ||
				hitData.Data.GetValue<bool>(HitDataIdentifiers.DEFLECTED);

			// A charge is worthless until it connects; pay once per swing for the charge that was delivered.
			if (!chargeRewarded && accumulatedChargePoints > 0f && !neglected)
			{
				chargeRewarded = true;
				statHandler.RewardExpPoints(Element.Light, accumulatedChargePoints, ExpSources.STATIC_HIT);
			}

			// Only the spite that actually finished someone.
			if (maliceDrained > 0f && hitData.Data.GetValue<bool>(HitDataIdentifiers.KILLED))
			{
				statHandler.RewardExpPoints(Element.Void, maliceDrained, ExpSources.MALICE_KILL);
			}
		}

		protected virtual void ProcessHit(IHittable hittable, HitData hitData)
		{
			if (hittable.Hit(hitData))
			{
				// This statement is entered after the target has fully processed the hit,
				// meaning all return data is present in the hitData.

				float force = hitData.Data.GetValue<float>(HitDataIdentifiers.FORCE);

				// Shed our share of the closing momentum from the inelastic clash (computed receiver-side).
				// Zero on a neglected hit (block/parry/deflect handles braking explicitly below).
				rigidbodyWrapper.AddForce(
					hitData.Data.GetValue(HitDataIdentifiers.INERTIA_BRAKE, Vector3.zero),
					ForceMode.VelocityChange);

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
				else
				{
					// Hit landed (not blocked/parried/deflected). Reward the attacker's Static (NE).
					if (chargeStat != null)
					{
						// Transducer: grounded force (Mass × Power) → charge, a fraction of what a parry refunds.
						chargeStat.BaseValue += hitData.Mass * hitData.Power * combatSettings.StaticGain * combatSettings.HitStaticPercent;
					}

					// A precise strike grounds its own charge: crits are Pierce-gated, so refuel Static off PIERCE —
					// a Power-independent self-sustain for Light builds (Pierce → crit → Static → Pierce charge).
					if (hitData.Data.GetValue<bool>(HitDataIdentifiers.CRIT))
					{
						statHandler.PointStats.NE.Current.BaseValue += hitData.Pierce * combatSettings.StaticGain * combatSettings.CritStaticPercent;
					}
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
				agentImpactHandler.ReportImpact(new ImpactData
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
