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
		// Exertion cost (mass curve + its constants) lives in CombatSettings — the AI has to price a swing before
		// performing it, same reason the wield speed curve moved there.
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

		protected IMeleeCombatMove move;
		protected CallbackService callbackService;
		protected TransformLookup transformLookup;
		protected ITargeter targeter;
		protected ITargetable agentTargetable;
		protected IAgentMovementHandler movementHandler;
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

		/// <summary>Limb mass blended toward whole-body by the move's BodyMassFraction, via the combat authority.</summary>
		private float StrikeMass => combatComponent.ComputeStrikeMass(move);
		private EntityStat pierceStat;
		private EntityStat luckStat;
		private EntityStat chargeStat;
		private EntityStat chargeSpeedStat;
		private EntityStat performSpeedStat;
		private EntityStat stormSpeedStat;
		private EntityStat stickAimStat;
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
		private ITargetable target;
		private float maxStick;
		private float maxReach;
		private bool stickTravelling;
		private float stickTravelTimer;
		private float stickDesired;
		private Vector3 stickHeading;
		private float stickSpeed;
		private float stickBudget;
		private float stickTravelled;
		private Vector3 stickLastPosition;
		private float stickLastDistance;
		private float stickClosingRate;
		private bool stickBraking;
		private float stickBrakeLength;
		private float stickBrakeTimer;
		private bool stickBrakeMoving;
		private int stickDriveStep = -1;
		private bool hasStorm;

		/// <summary>Multiple of the ideal travel time before the approach's hard backstop fires.</summary>
		private const float travelTimeoutSlack = 2f;

		/// <summary>Speed (m/s) at which the plant hands back to normal movement. Absolute, not a fraction of
		/// entry — that is what makes a fast lunge brake for longer than a slow step.</summary>
		private const float brakeHandback = 1.5f;

		/// <summary>EMA rate for the measured closing rate (higher = snappier; ~1/rate s time-constant).</summary>
		private const float closingSmoothRate = 20f;
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
			stickAimStat = Agent.Stats.GetStat(AgentStatIdentifiers.STICK_AIM, false);
			enduranceCostStat = Agent.Stats.GetStat(AgentStatIdentifiers.ENDURANCE.SubStat(AgentStatIdentifiers.SUB_DRAIN));
			poiseStat = Agent.Stats.GetStat(AgentStatIdentifiers.POISE, true);
		}

		public override void Start()
		{
			base.Start();

			hitDetector = new CombatHitDetector(Agent, transformLookup, move, hitDetectionMask);
			Performer.StartedPerformingEvent += OnStartedPerformingEvent;

			// LateUpdate so the sweep reads bones AFTER the Animator bakes this frame's pose. Casting from
			// Update reads the previous bake, lagging the blade by a frame of RunTime (= dt x timescale).
			callbackService.SubscribeUpdate(UpdateMode.LateUpdate, this, LateUpdateHitDetection);

			totalCharge = 1f;
			accumulatedChargePoints = 0f;
			chargeRewarded = false;
			maxStick = 0f;
			maxReach = 0f;
			stickTravelling = false;
			stickBraking = false;

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
				// Natural strike (kick, body ram): nothing to wield, so it swings at its designed speed & power.
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

			// A performance cancelled mid-travel (blocked, parried, stunned) must never leave the performer paused.
			if (stickTravelling)
			{
				stickTravelling = false;
				Performer.Paused = false;
			}

			stickBraking = false;
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

				// Begin the approach once the move's own delay has passed.
				if (inertiaTimer != null && inertiaTimer.Expired)
				{
					BeginApproach();
					inertiaTimer.Dispose();
					inertiaTimer = null;
				}

				// Drive it, and hold the swing until it has arrived (or clearly won't).
				if (stickTravelling)
				{
					UpdateApproach(delta);
				}
			}
			else
			{
				if (swingPhaseSpeedMod != null)
				{
					swingPhaseSpeedMod.SetValue(1f);
				}

				stickTravelling = false;
			}

			// The plant outlives the drive and plays out through the swing, so it ticks in every state.
			if (stickBraking)
			{
				UpdateBrake(delta);
			}

			// Hit detection ticks in LateUpdateHitDetection, not here.

			if (Performer.State is PerformanceState.Finishing && !stickBraking)
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
				// Driven by SPEED, not raw acceleration — acceleration spikes into the thousands during the drive
				// and the plant, which unbounded sent the shake frequency two orders past its default.
				stormShake.Direction = -rigidbodyWrapper.Velocity.normalized;
				stormShake.Intensity = Mathf.Clamp01(rigidbodyWrapper.Speed / Mathf.Max(stickSpeed, 0.01f));
				stormShake.Frequency = IShakeSource.DEFAULT_FREQUENCY * (0.5f + 0.5f * stormShake.Intensity);
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

			// Two bodies pushing — runs regardless of whether anything has been hit.
			EnforceBodyClash();
		}

		/// <summary>
		/// Hit detection, in LateUpdate so the sweep reads the skeleton after the Animator bakes this frame's pose.
		/// Ticks every frame to keep the stored orientation fresh, but only casts past HitDetectionDelay.
		/// </summary>
		private void LateUpdateHitDetection(float delta)
		{
			bool detectHits = Performer.State == PerformanceState.Performing && Performer.RunTime >= move.HitDetectionDelay;
			bool firstScan = detectHits && !wasScanning;

			// First detection frame starts at the interpolated delay crossing, not the full previous frame —
			// otherwise a coarse frame step starts detection earlier.
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
		/// Two masses pushing, dead by design — no bounce, no stats. Runs the whole attack and forbids the body from
		/// pushing deeper than <see cref="AgentCombatComponent.ComputeBodyFloor"/>. Perfectly inelastic.
		/// </summary>
		private void EnforceBodyClash()
		{
			if (target == null || agentTargetable == null)
			{
				return;
			}

			Vector3 toTarget = (target.Position - rigidbodyWrapper.Position).FlattenY();
			float distance = toTarget.magnitude;
			if (distance <= 0.0001f || distance >= combatComponent.ComputeBodyFloor(move, target.Radius))
			{
				return;
			}

			Vector3 inward = toTarget / distance;
			RigidbodyWrapper other = target.Entity != null &&
				target.Entity.TryGetEntityComponent(out IAgentBody body) && body.HasRigidbody
				? body.RigidbodyWrapper
				: null;

			// Predicted, since this runs at render rate and must see what it already queued this physics step.
			float closing = Vector3.Dot(rigidbodyWrapper.PredictedVelocity, inward);
			if (other != null)
			{
				closing -= Vector3.Dot(other.PredictedVelocity, inward);
			}

			if (closing <= 0f)
			{
				// Already separating (or being carried out) — nothing to resolve.
				return;
			}

			if (other == null)
			{
				// Immovable target: we absorb all of it.
				rigidbodyWrapper.AddForce(-inward * closing, ForceMode.VelocityChange);
				return;
			}

			// Split the cancellation by mass — the heavier body gives way least.
			float total = rigidbodyWrapper.Mass + other.Mass;
			rigidbodyWrapper.AddForce(-inward * (closing * other.Mass / total), ForceMode.VelocityChange);
			other.AddForce(inward * (closing * rigidbodyWrapper.Mass / total), ForceMode.VelocityChange);
		}

		/// <summary>
		/// Best-scoring enemy inside the acquisition cone around the held direction, within <see cref="maxReach"/>.
		/// Aim leads the score, so the one at your elbow can't steal a deliberate swing at someone further.
		/// </summary>
		private ITargetable AcquireStickTarget()
		{
			// TargetVelocity is what the agent is steering toward: the held direction, before we overwrite it.
			Vector3 held = rigidbodyWrapper.TargetVelocity.FlattenY();
			Vector3 aim = held.sqrMagnitude > 0.0001f
				? held.normalized
				: rigidbodyWrapper.Forward.FlattenY().normalized;

			float cosLimit = Mathf.Cos(combatSettings.StickAcquireAngle * Mathf.Deg2Rad);
			float coverage = maxReach;
			ITargetable best = null;
			float bestScore = float.MinValue;

			foreach (ITargetable candidate in targeter.Enemies.Components)
			{
				if (candidate == null)
				{
					continue;
				}

				Vector3 toCandidate = (candidate.Position - rigidbodyWrapper.Position).FlattenY();
				float distance = toCandidate.magnitude;
				if (distance < 0.0001f)
				{
					return candidate;
				}

				// Only ever consider what this swing could actually land on.
				if (distance > coverage + candidate.Radius)
				{
					continue;
				}

				float alignment = Vector3.Dot(toCandidate / distance, aim);
				if (alignment < cosLimit)
				{
					continue;
				}

				// Distance only breaks ties between similarly-aimed enemies.
				float score = alignment - 0.1f * (distance / Mathf.Max(coverage, 0.01f));
				if (score > bestScore)
				{
					bestScore = score;
					best = candidate;
				}
			}

			return best;
		}

		/// <summary>
		/// Flight time of a leap covering <paramref name="distance"/>, scaling with √distance (the real jump
		/// relationship). <see cref="CombatSettings.StickTime"/> is the airtime of a FULL-range leap.
		/// </summary>
		private float LeapTime(float distance)
		{
			float range = Mathf.Max(combatComponent.ComputeStickRange(move), 0.01f);
			return Mathf.Max(
				combatSettings.StickTime * Mathf.Sqrt(Mathf.Clamp01(distance / range)),
				0.01f);
		}

		/// <summary>
		/// Speed of a leap covering <paramref name="distance"/>; peaks at range / StickTime. Goes as
		/// √(distance × range), so Stick_Range lengthens AND quickens the leap while load shortens and slows it.
		/// </summary>
		private float LeapSpeed(float distance)
		{
			return distance / LeapTime(distance);
		}

		/// <summary>
		/// Begins the leap — one mechanism for stick and storm. Drives a heading until arrival, then releases the
		/// held swing. Nothing to leap at: the swing goes at once, carrying StickIdleInertia of a full leap.
		/// </summary>
		private void BeginApproach()
		{
			Vector3 toTarget = target != null && agentTargetable != null
				? (target.Position - rigidbodyWrapper.Position).FlattenY()
				: Vector3.zero;
			float distance = toTarget.magnitude;
			bool aimed = distance > 0.0001f;

			stickHeading = aimed ? toTarget / distance : rigidbodyWrapper.Forward.FlattenY().normalized;
			if (stickHeading.sqrMagnitude < 0.0001f)
			{
				OnSwing();
				return;
			}

			// Land inside our own reach rather than at its very tip — StickBite deep, floored at the attack-pose
			// margin so the leap never aims into the target's body. Shared with the AI's strike timing.
			float desired = aimed ? combatComponent.ComputeStickDesired(move, target.Radius) : 0f;
			float gap = aimed ? distance - desired : 0f;

			if (gap <= 0f)
			{
				// Nothing to leap at: swing out at once, carrying a fraction of a full leap's speed. It plants
				// exactly like a leap does — the two only differ in how they get moving, never in how they stop.
				float idleSpeed = LeapSpeed(combatComponent.ComputeStickRange(move)) * combatSettings.StickIdleInertia;
				rigidbodyWrapper.Push(stickHeading * idleSpeed);
				OnSwing();
				BeginPlant(idleSpeed);
				return;
			}

			// Clamped, not gated: past the budget the leap saturates and falls short. The budget is the DRIVE;
			// planting the feet happens once it has been crossed, so the trajectory is StickRange + a short tail.
			float cover = Mathf.Min(gap, maxStick);
			// Leap speed is sized off stick range, not the storm's extended cover, so a long storm isn't handed an
			// absurd speed. Max: a high Stick_Range leap can outrun StormSpeed, and a storm must never close slower.
			float leapSpeed = LeapSpeed(Mathf.Min(cover, combatComponent.ComputeStickRange(move)));
			stickSpeed = hasStorm
				? Mathf.Lerp(leapSpeed,
					Mathf.Max(combatSettings.StormSpeed * (stormSpeedStat ?? 1f), leapSpeed),
					combatComponent.ChargeFraction(totalCharge))
				: leapSpeed;
			stickSpeed = Mathf.Max(stickSpeed, 0.01f);
			stickDesired = desired;
			stickLastDistance = distance;
			// Before the gap has moved, we are the only thing closing it.
			stickClosingRate = stickSpeed;
			// THE range cap: sizing speed to 'cover' doesn't bound distance, so meter real ground against a budget.
			stickBudget = cover;
			stickTravelled = 0f;
			stickLastPosition = rigidbodyWrapper.Position;
			// Backstop; arrival and exhaustion both release sooner.
			stickTravelTimer = cover / stickSpeed * travelTimeoutSlack;
			stickDriveStep = -1;
			stickTravelling = true;


			// Hold the swing across the gap — Paused freezes RunTime, so the wind-up pose holds and the
			// hit-detection delay isn't spent in transit (else the blade sweeps mid-flight and whiffs).
			movementHandler.AutoUpdateMovement = false;
			Performer.Paused = true;
		}

		/// <summary>
		/// Drives the leap, releasing the swing on whichever comes first: ARRIVED (measured closing rate puts the
		/// blade on target), SPENT (budget used up) or TIMEOUT — which is also what a dodging target rides out.
		/// </summary>
		private void UpdateApproach(float delta)
		{
			// Meter real displacement, not speed × time, so anything that slowed the leap is accounted for.
			Vector3 position = rigidbodyWrapper.Position;
			stickTravelled += (position - stickLastPosition).FlattenY().magnitude;
			stickLastPosition = position;

			stickTravelTimer -= delta;
			bool release = stickTravelTimer <= 0f || stickTravelled >= stickBudget;

			if (!release && target != null)
			{
				Vector3 toTarget = (target.Position - rigidbodyWrapper.Position).FlattenY();
				float distance = toTarget.magnitude;
				if (distance > 0.0001f)
				{
					Vector3 toTargetDir = toTarget / distance;

					// How fast the GAP shrinks, not how fast WE move — a target leaping in closes it at the sum
					// of both, so our own speed alone predicts ~double the time we have and releases late.
					float rawClosing = delta > 0.0001f ? (stickLastDistance - distance) / delta : stickClosingRate;
					stickClosingRate = Mathf.Lerp(stickClosingRate, rawClosing, Mathf.Clamp01(closingSmoothRate * delta));
					stickLastDistance = distance;

					// Storm homes outright; a leap is committed and only bent, at the Acuity-scaled turn rate.
					if (hasStorm)
					{
						stickHeading = toTargetDir;
					}
					else
					{
						float aim = stickAimStat ?? 0f;
						if (aim > 0f)
						{
							stickHeading = Vector3.RotateTowards(
								stickHeading,
								toTargetDir,
								aim * combatSettings.StickTurnRate * Mathf.Deg2Rad * delta,
								0f).normalized;
						}
					}

					// Release EARLY by the hit-window delay so the blade lands WITH the body.
					float swingLead = move.HitDetectionDelay /
						Mathf.Max(performSpeedStat != null ? performSpeedStat.Value : 1f, 0.01f);
					// A stalled or retreating gap gives no arrival time — budget and timeout own those cases.
					float remaining = distance - stickDesired;
					float timeToArrive = remaining <= 0f ? 0f
						: stickClosingRate > 0.01f ? remaining / stickClosingRate
						: float.MaxValue;

					release = timeToArrive <= swingLead;
				}
			}

			if (release)
			{
				EndApproach();
				return;
			}

			rigidbodyWrapper.TargetVelocity = stickHeading * stickSpeed;

			// Per-physics-step force; calling it every render frame stacks it inside one step.
			if (rigidbodyWrapper.PhysicsStep != stickDriveStep)
			{
				stickDriveStep = rigidbodyWrapper.PhysicsStep;
				rigidbodyWrapper.ApplyMovement(null, maxAcceleration, maxDeceleration, power, true);
			}

			movementHandler.ForceRotation(stickHeading);
		}

		/// <summary>
		/// Lands the leap: releases the held swing and hands over to the plant, which plays out through the swing.
		/// Arrival velocity is KEPT so the strike carries the body's momentum into contact.
		/// </summary>
		private void EndApproach()
		{
			stickTravelling = false;
			Performer.Paused = false;
			OnSwing();

			BeginPlant(stickSpeed);
		}

		/// <summary>
		/// Starts the plant. <paramref name="entrySpeed"/> is the INTENDED speed — an impulse only resolves on the
		/// next physics step, so the rigidbody cannot be asked how fast it is about to be going.
		/// </summary>
		private void BeginPlant(float entrySpeed)
		{
			stickBrakeLength = combatSettings.StickPlant * combatSettings.StickRange;

			if (entrySpeed <= brakeHandback || stickBrakeLength <= 0.0001f)
			{
				ReleaseMovement();
				return;
			}

			// Time for quadratic drag to fall from entry to handback, + slack: t = L(1/v1 - 1/v0).
			stickBrakeTimer = stickBrakeLength * (1f / brakeHandback - 1f / entrySpeed) * 2f;
			stickBrakeMoving = false;
			stickBraking = true;
			movementHandler.AutoUpdateMovement = false;
		}

		/// <summary>
		/// Plants the feet with QUADRATIC drag (a = v²/L), so braking force grows with the square of speed — a fast
		/// lunge is killed hard while a slow step is barely touched. Only the agent's own forward momentum.
		/// </summary>
		private void UpdateBrake(float delta)
		{
			// Predicted, or every render frame in a step brakes off the same stale speed.
			float speed = Vector3.Dot(rigidbodyWrapper.PredictedVelocity.FlattenY(), stickHeading);
			stickBrakeTimer -= delta;

			// The push lands on the next physics step, so the opening frames can still read the old velocity.
			// Only let the handback finish the plant once real motion has actually been seen.
			stickBrakeMoving |= speed > brakeHandback;

			// speed <= 0 hands back unconditionally: below zero the drag integral AMPLIFIES instead of braking.
			if (stickBrakeTimer <= 0f || speed <= 0f || (stickBrakeMoving && speed <= brakeHandback))
			{
				ReleaseMovement();
				return;
			}

			// Exact integral of dv/dt = -v²/L, so the curve is identical at any frame rate.
			float next = speed / (1f + speed * delta / stickBrakeLength);
			rigidbodyWrapper.AddForce(stickHeading * (next - speed), ForceMode.VelocityChange);
			rigidbodyWrapper.TargetVelocity = Vector3.zero;
		}

		private void ReleaseMovement()
		{
			stickBraking = false;
			rigidbodyWrapper.TargetVelocity = Vector3.zero;
			movementHandler.AutoUpdateMovement = true;
		}

		protected void OnStartedPerformingEvent(IPerformer performer)
		{
			stickTravelling = false;
			stickBraking = false;

			// Storm is the same leap with a charge-extended budget — charge buys reach on top of the stick.
			float storm = combatComponent.ComputeStormRange(move, totalCharge);
			hasStorm = storm > 0f;
			maxStick = combatComponent.ComputeStickRange(move) + storm;
			// ABSOLUTE maximum reach of this swing, leap included — the acquisition horizon. Taken from the combat
			// authority so it can't drift from the reach the AI's fire gate uses.
			maxReach = combatComponent.ComputeEffectiveReach(move) + storm;

			// TARGETING: a hard lock always wins, otherwise acquire whoever the swing is aimed at. Must run
			// BEFORE TargetVelocity is overwritten below - that vector IS the held direction.
			target = targeter.Target ?? AcquireStickTarget();

			if (target != null)
			{
				rigidbodyWrapper.TargetVelocity = (target.Position - RigidbodyWrapper.Position).normalized;
			}
			movementHandler.ForceRotation(null, Agent.Mind.Personality.E.OutQuad());

			if (hasStorm && Agent.Identification.HasAll(EntityLabels.PLAYER))
			{
				// Scaled by the charge so a light overcharge stays subtle and a full one screams.
				stormShake = new ContinuousShakeSource(
					stormShakeMagnitude * Mathf.Clamp01(totalCharge - 1f),
					-rigidbodyWrapper.TargetVelocity);
				agentImpactHandler.ReportImpact(new ImpactData
				{
					Source = Agent,
					Direction = -rigidbodyWrapper.TargetVelocity,
					Location = Agent.Transform.position,
					ShakeSource = stormShake
				});
			}

			inertiaTimer = new TimerClass(move.InertiaDelay, () => timescaleStat, callbackService);

			// Exertion: priced on LIMB mass by the combat authority, not the strike's mass — what a swing costs the
			// body is what it wields, while StrikeMass is what the hit lands with. Play the audio off the drain.
			float drained = statHandler.PointStats.N.Drain(combatComponent.ComputePerformCost(move));
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
					// The strike's own travel direction IS the knockback direction; only a geometry-less move
					// (zero-length StrikeDirection) falls back to the swept contact normal.
					Vector3 direction = move.StrikeDirection.sqrMagnitude > 0.0001f
						? move.StrikeDirection.Look(lookDir)
						: hit.Direction;

					float mass = StrikeMass;
					float phase = Mathf.Clamp01(Performer.RunTime / Move.MinDuration);
					float phaseMult = GetPhaseInertiaMultiplier(phase);

					// BODY momentum only. The strike's force is the receiver's second channel — it needs their
					// coupling and endurance wear to size it. Predicted, so the plant's queued braking counts.
					Vector3 inertia = rigidbodyWrapper.PredictedVelocity;

					// Per-axis base output (x=Slash, y=Power, z=Pierce) from AgentCombatComponent.
					// Runtime modifiers (strength, charge, phase, malice) are applied below.
					Vector3 baseOutput = combatComponent.GetMoveOutput(move).Output;

					float basePower = baseOutput.y * baseStrengthPowerFactor;
					float powerValue = basePower * totalCharge * phaseMult;

					// Assemble the offence vector's runtime-modified channels before Malice.
					float slashValue = baseOutput.x;
					float pierceValue = baseOutput.z + (accumulatedChargePoints * chargeDamageEfficiency);

					// MALICE: spite scales the WHOLE offence vector. Coverage = the fraction the pool pays for
					// (Drain applies the Hostility-driven DrainMult). Symmetrical to Grace.
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

				// Our share of the strike's force, bounced back off the target's footing (computed receiver-side).
				// Zero on a neglected hit (block/parry/deflect handles braking explicitly below).
				rigidbodyWrapper.Push(hitData.Data.GetValue(HitDataIdentifiers.INERTIA_BRAKE, Vector3.zero));

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
