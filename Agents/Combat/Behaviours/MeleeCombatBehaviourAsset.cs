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
	public class MeleeCombatBehaviourAsset : BaseCombatMoveBehaviourAsset, IChargeProvider, ILungeProvider
	{
		/// <inheritdoc/>
		public float ChargeMultiplier => totalCharge;

		/// <inheritdoc/>
		public float ChargeFraction => chargeCeiling > 0f ? Mathf.Clamp01(chargePoints / chargeCeiling) : 0f;

		/// <inheritdoc/>
		public bool ChargeDepleted => chargeDepleted;

		/// <inheritdoc/>
		public float ChargeSpent => chargePool > 0f ? Mathf.Clamp01(rawChargeSpent / chargePool) : 0f;

		/// <inheritdoc/>
		public float AutoReleaseProgress => !chargeDepleted ? 0f
			: combatSettings.ChargeEmptyGrace > 0f ? Mathf.Clamp01(chargeGraceTimer / combatSettings.ChargeEmptyGrace) : 1f;

		/// <summary>The charge as the STORM sees it: 0 below the deadzone, ramping to 1 - the single storm gate.</summary>
		private float StormCharge => combatComponent.StormCharge(ChargeFraction);

		/// <inheritdoc/>
		public bool Lunging => stickTravelling;

		public TrailSettings StormTrail => stormTrail;

		/// <summary>
		/// How much of the gap has closed, not how long it has taken - a slow leap and a fast one both read
		/// the same fraction of the way in, which is what lets the region lead into the swing either way.
		/// </summary>
		public float LungeProgress => stickGap > 0.0001f
			? Mathf.Clamp01(1f - stickRemaining / stickGap)
			: 1f;

		[Header("Hit Detection")]
		[SerializeField] private LayerMask hitDetectionMask;

		[Header("Swinging")]
		// Wield speed, heavy slow start and exertion cost live in CombatSettings: the AI reads them before the swing.
		[SerializeField] private float swingShakeMagnitude = 1.5f;

		// The whole charge economy (power, pierce, efficiency, grace) lives in CombatSettings.

		[Header("Charging")]
		[SerializeField, Tooltip("Player camera rumble strength while Static is drawn in, scaled by the pool spent. 0 = none.")]
		private float chargeShakeStrength = 0.5f;
		[SerializeField, Tooltip("Player camera kick on release, scaled by the pool spent. X/Y push along the storm, so a release without one uses Z only.")]
		private Vector3 releaseShakeMagnitude = Vector3.one * 2f;
		[SerializeField, Tooltip("Seconds the release kick takes to fade out, linearly.")]
		private float releaseShakeDuration = 0.3f;

		[Header("Storming")]
		[SerializeField] private float maxAcceleration = 20000f;
		[SerializeField] private float maxDeceleration = 2000f;
		[SerializeField] private float power = 50f;
		[SerializeField] private Vector3 stormShakeMagnitude = Vector3.one * 2;
		[SerializeField] private TrailSettings stormTrail = new TrailSettings();
		[Header("Lunging")]
		[SerializeField, Tooltip("Faint trail on a lunge that actually SPENT Stamina - the tell that commitment cost something. Author it like the dash trail. A storm overrides it with its own.")]
		private TrailSettings lungeTrail = new TrailSettings();

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
		private TimedCurveModifier hitPauseMod;
		private float totalCharge;
		private float chargePoints; // Points STORED after efficiency decay - what the swing actually uses.
		private float rawChargeSpent; // Raw Static paid for them; the deed EXP is priced off this.
		private float chargeReference; // Points per efficiency halving, pool-scaled.
		private float chargeCeiling; // Most points this pool could ever store.
		private float chargePool; // Static max the charge is measured against.
		private bool chargeDepleted;
		private float chargeGraceTimer;
		private bool chargeRewarded; // Whether this swing's charge has already paid Light EXP.
		private ITargetable target;
		private float maxStick;
		private float maxReach;
		private bool stickTravelling;
		private IStormFeedback stormFeedback;
		private ILungeFeedback lungeFeedback;
		private AgentTrailEffect agentTrailEffect;
		private bool storming;
		private bool trailing;
		private bool lungePaid;
		private float stickTravelTimer;
		private float stickDesired;
		private Vector3 stickHeading;
		private Vector3 stickAim;
		private float lungeCommit;
		private bool stickSwung;
		private Vector3 stickLaunchForward;
		private float stickTurnSpeed;
		private float stickSpeed;
		private float stickBudget;
		private float stickTravelled;
		private float stickGap;
		private float stickRemaining;
		private bool inertiaPending;
		private float pendingInertia;
		private Vector3 stickLastPosition;
		private float stickLastDistance;
		private float stickClosingRate;
		private bool stickBraking;
		private float stickBrakeLength;
		private float stickBrakeTimer;
		private bool stickBrakeMoving;
		private int stickDriveStep = -1;
		private bool hasStorm;
		private bool contactReached;
		private float contactSpeed; // Forward speed as the blade reached its contact point.

		/// <summary>Multiple of the ideal travel time before the approach's hard backstop fires.</summary>
		private const float travelTimeoutSlack = 2f;

		/// <summary>Speed (m/s) at which the plant hands back to normal movement. Absolute, not a fraction of
		/// entry — that is what makes a fast lunge brake for longer than a slow step.</summary>
		private const float brakeHandback = 1.5f;

		/// <summary>EMA rate for the measured closing rate (higher = snappier; ~1/rate s time-constant).</summary>
		private const float closingSmoothRate = 20f;
		private ContinuousShakeSource swingShake;
		private ContinuousShakeSource stormShake;
		private ContinuousShakeSource chargeShake;
		private ContinuousShakeSource releaseShake;
		private float releaseShakeTimer;

		// Strength/mass derived per-swing values.
		private float wieldShortfall;
		private float appliedPhaseMult = 1f;
		private float baseStrengthSpeedFactor = 1f;

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
			AgentAudioHandler agentAudioHandler,
			[Optional] IStormFeedback stormFeedback,
			[Optional] ILungeFeedback lungeFeedback,
			[Optional] AgentTrailEffect agentTrailEffect)
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
			this.stormFeedback = stormFeedback;
			this.lungeFeedback = lungeFeedback;
			this.agentTrailEffect = agentTrailEffect;

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
			chargePoints = 0f;
			rawChargeSpent = 0f;
			chargeDepleted = false;
			chargeGraceTimer = 0f;
			chargeRewarded = false;

			// Pool-scaled, so the efficiency curve and the time to burn the pool are the same at any rank.
			float staticMax = statHandler.ResourceStats.NE.Max;
			chargePool = staticMax;
			chargeReference = CombatUtils.ChargeReference(staticMax, combatSettings.ChargeEfficiencyDecay);
			chargeCeiling = CombatUtils.ChargeCeiling(staticMax, chargeReference);
			maxStick = 0f;
			maxReach = 0f;
			stickTravelling = false;
			stickBraking = false;
			inertiaPending = false;
			pendingInertia = 0f;

			// Wield is judged on the WEAPON alone, once per behaviour instance: the arm you were born with
			// never counts against you, and a natural strike (kick, ram) carries no weapon at all.
			float weaponMass = limbMassStat == null
				? 0f : SpaxFormulas.WeaponMass((float)limbMassStat, rigidbodyWrapper.Mass);
			wieldShortfall = combatSettings.WieldShortfall(strengthStat, weaponMass);
			baseStrengthSpeedFactor = combatSettings.WieldSpeedFactor(strengthStat, weaponMass);

			// Base strength-speed modifier (constant over the swing).
			speedMod = new FloatFuncModifier(
				ModMethod.Absolute,
				f => f * baseStrengthSpeedFactor
			);
			chargeSpeedStat?.AddModifier(speedMod);
			performSpeedStat?.AddModifier(speedMod);

			// Phase-based inertia modifier (varies during swing).
			swingPhaseSpeedMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			appliedPhaseMult = 1f;
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

			// A cancelled swing never gets its lurch.
			inertiaPending = false;

			stormShake?.Dispose();
			swingShake?.Dispose();
			chargeShake?.Dispose();
			releaseShake?.Dispose();
			chargeShake = null;
			releaseShake = null;
			agentAudioHandler.StopStrain(this);
			UpdateStormFeedback();
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

			Performer.Prolong = RigidbodyWrapper.Speed > move.ProlongThreshold;

			if (Performer.State == PerformanceState.Preparing && !Performer.Canceled)
			{
				UpdateChargeAim(delta);
			}
			else
			{
				// Released or interrupted: the strain ends now, so a stun's grunt isn't swallowed by it.
				agentAudioHandler.StopStrain(this);
			}

			if (Performer.State == PerformanceState.Preparing && Performer.ChargeTime >= Move.MinCharge)
			{
				if (chargeDepleted)
				{
					// Pool empty: the warning loops, then the swing leaves on its own.
					chargeGraceTimer += delta;
					if (chargeGraceTimer >= combatSettings.ChargeEmptyGrace)
					{
						Performer.TryPerform();
					}
				}
				else
				{
					// Points are STORED at a constant rate (Pierce), so progress is timeable; each one costs
					// more Static than the last, which is what makes the drain accelerate.
					float speed = chargeSpeedStat != null ? chargeSpeedStat.Value : 1f;
					float gain = pierceStat * delta * speed;
					float cost = CombatUtils.ChargeCost(chargePoints, gain, chargeReference);

					float paid = statHandler.ResourceStats.NE.Drain(cost, out bool drained);
					rawChargeSpent += paid;
					chargePoints = CombatUtils.ChargeStore(chargePoints, paid, chargeReference);
					totalCharge = 1f + chargePoints * combatSettings.ChargePowerPerPoint;

					if (drained)
					{
						// The strain is the effort of drawing Static in, so it ends the moment the pool is dry.
						chargeDepleted = true;
						chargeGraceTimer = 0f;
						agentAudioHandler.StopStrain(this);
					}
					else if (ChargeSpent > 0f)
					{
						agentAudioHandler.Strain(this, ChargeSpent);
					}
				}

				UpdateChargeShake();
			}

			if (Performer.State == PerformanceState.Performing)
			{
				// Phase-based inertia: heavy swings start slow, then catch up.
				float phaseMult = GetPhaseInertiaMultiplier(SwingPhase(Performer.RunTime));
				if (swingPhaseSpeedMod != null)
				{
					swingPhaseSpeedMod.SetValue(phaseMult);
					appliedPhaseMult = phaseMult;
				}

				// The swing's own forward lurch, fired off RunTime rather than a wall clock so it lands on
				// the INERTIA marker exactly - InertiaDelay IS that marker's offset from the swing's start.
				if (inertiaPending && Performer.RunTime >= move.InertiaDelay)
				{
					inertiaPending = false;
					ApplyInertia();
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

				// The swing can run out from under an UNTARGETED leap, whose RunTime is never paused. Dropping the
				// drive here would strand movement disabled and skip the plant, so land it properly instead.
				if (stickTravelling)
				{
					EndApproach();
				}
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
			UpdateStormFeedback();
			if (stormShake != null)
			{
				// Driven by SPEED, not raw acceleration — acceleration spikes into the thousands during the drive
				// and the plant, which unbounded sent the shake frequency two orders past its default.
				stormShake.Direction = -rigidbodyWrapper.Velocity.normalized;
				stormShake.Intensity = Mathf.Clamp01(rigidbodyWrapper.Speed / Mathf.Max(stickSpeed, 0.01f));
				stormShake.Frequency = IShakeSource.DEFAULT_FREQUENCY * (0.5f + 0.5f * stormShake.Intensity);
			}
			if (releaseShake != null)
			{
				releaseShakeTimer += delta;
				float fade = releaseShakeDuration > 0f ? 1f - releaseShakeTimer / releaseShakeDuration : 0f;
				if (fade > 0f)
				{
					releaseShake.Intensity = ChargeSpent * fade;
				}
				else
				{
					releaseShake.Dispose();
					releaseShake = null;
				}
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

			// What a late hit is measured against: the body's speed as the blade reached its contact point.
			if (!contactReached && !stickTravelling && Performer.State == PerformanceState.Performing &&
				Performer.RunTime >= move.ContactTime)
			{
				contactReached = true;
				contactSpeed = HeadingSpeed();
			}

			if (hitDetector.Update(detectHits, sweepStart, out List<HitScanHitData> newHits))
			{
				OnNewHitDetected(newHits);
			}
		}

		/// <summary>Forward speed along the lunge heading; predicted, so this frame's queued plant and clash count.</summary>
		private float HeadingSpeed() => Vector3.Dot(rigidbodyWrapper.PredictedVelocity.FlattenY(), stickHeading);

		/// <summary>
		/// Output the strike still delivers: a lunge's forward body share (BMF × thrust) rides on its momentum once
		/// the arm has fully extended at contact. Never above 1.
		/// </summary>
		private float MomentumCarry()
		{
			float bodyShare = move.BodyMassFraction * Mathf.Clamp01(move.StrikeDirection.z);

			// Never lunged, or already standing by contact: it hits like a planted swing, with no momentum to lose.
			if (bodyShare <= 0f || stickGap <= 0f || !contactReached || contactSpeed <= brakeHandback)
			{
				return 1f;
			}

			return 1f - bodyShare * (1f - Mathf.Clamp01(HeadingSpeed() / contactSpeed));
		}

		/// <summary>
		/// Swing speed by phase: a heavy swing starts slow, then catches up. Speed only, never power.
		/// </summary>
		private float GetPhaseInertiaMultiplier(float phase)
			=> combatSettings.PhaseSpeedFactor(wieldShortfall, phase);

		/// <summary>Eased swing phase at <paramref name="runTime"/>; what the phase inertia is driven by.</summary>
		private float SwingPhase(float runTime)
			=> CombatSettings.SwingPhase(runTime, Move.MinDuration);

		/// <summary>
		/// Real seconds until the swing's RunTime reaches <paramref name="runTime"/>, integrating the phase
		/// speed-up rather than assuming the current (slowest) speed holds.
		/// </summary>
		private float TimeUntilRunTime(float runTime)
		{
			float from = Performer.RunTime;
			if (runTime <= from)
			{
				return 0f;
			}

			const int STEPS = 8;
			float baseSpeed = PerformSpeed / Mathf.Max(appliedPhaseMult, 0.01f);
			float step = (runTime - from) / STEPS;
			float time = 0f;
			for (int i = 0; i < STEPS; i++)
			{
				float mid = from + (i + 0.5f) * step;
				time += step / Mathf.Max(baseSpeed * GetPhaseInertiaMultiplier(SwingPhase(mid)), 0.01f);
			}
			return time;
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

		/// <summary>The forward lurch every swing throws its body into, whether or not it leapt.</summary>
		private float LurchSpeed => LeapSpeed(combatComponent.ComputeStickRange(move)) * combatSettings.StickIdleInertia;

		/// <summary>Drag length (m) of the plant; see <see cref="UpdateBrake"/>.</summary>
		private float PlantLength => combatSettings.StickPlant * combatSettings.StickRange;

		private float PerformSpeed => Mathf.Max(performSpeedStat != null ? performSpeedStat.Value : 1f, 0.01f);

		/// <summary>
		/// Ground covered <paramref name="time"/> seconds after release: the plant's drag from <paramref name="speed"/>,
		/// topped up once by the swing's lurch — the same two things <see cref="EndApproach"/> sets off.
		/// </summary>
		private float PredictPlantTravel(float speed, float time)
		{
			float length = PlantLength;
			if (length <= 0.0001f)
			{
				return speed * time;
			}

			// Drag covers L·ln(1 + v·t/L); the lurch is a push, so it only ever raises the speed to its own.
			float lurchAt = Mathf.Clamp(TimeUntilRunTime(move.InertiaDelay), 0f, time);
			float before = length * Mathf.Log(1f + speed * lurchAt / length);
			float after = speed / (1f + speed * lurchAt / length);
			if (lurchAt < time)
			{
				after = Mathf.Max(after, LurchSpeed);
			}

			return before + length * Mathf.Log(1f + after * (time - lurchAt) / length);
		}

		/// <summary>
		/// Storm feedback and trail follow the storm LEAP only; polled because travel ends in several places.
		/// </summary>
		private void UpdateStormFeedback()
		{
			bool active = hasStorm && stickTravelling;
			// A BOUGHT lunge trails too - the tell that Stamina went out the door - and keeps trailing through its
			// PLANT, since the body is still visibly sliding after the drive hands off. A storm outranks the look.
			bool trail = active || (lungePaid && (stickTravelling || stickBraking));

			if (active && !storming)
			{
				stormFeedback?.BeginStorm(stickHeading);
			}
			else if (!active && storming)
			{
				stormFeedback?.EndStorm();
			}
			storming = active;

			// Trail and smear go together - one window, one intensity, so the two never disagree.
			if (trail && !trailing)
			{
				// Faded by how much was COMMITTED, so it shows how much Stamina the lunge actually bought.
				agentTrailEffect?.Begin(this, active ? stormTrail : lungeTrail, active ? 1f : lungeCommit);
				if (!active)
				{
					lungeFeedback?.BeginLunge(stickHeading);
				}
			}
			else if (!trail && trailing)
			{
				agentTrailEffect?.End(this);
				lungeFeedback?.EndLunge();
			}
			trailing = trail;

			if (trail && !active && lungeFeedback != null)
			{
				// Commitment is WHAT it shows; speed is only how much of it is currently on screen.
				lungeFeedback.UpdateLunge(lungeCommit *
					Mathf.Clamp01(rigidbodyWrapper.Speed / Mathf.Max(stickSpeed, 0.01f)));
			}

			if (storming && stormFeedback != null)
			{
				stormFeedback.UpdateStorm(Mathf.Clamp01(rigidbodyWrapper.Speed / Mathf.Max(stickSpeed, 0.01f)));
			}
		}

		/// <summary>
		/// Aims at what is about to be leapt at while the swing is held, control fading out as the charge commits.
		/// </summary>
		private void UpdateChargeAim(float delta)
		{
			float commit = Performer.ChargeTime / Mathf.Max(0.01f, combatSettings.LungeAimCommitTime);
			float control = 1f - Mathf.Clamp01(commit);
			if (control <= 0f)
			{
				return;
			}

			ITargetable locked = targeter.Target;
			Vector3 aim = locked != null
				? (locked.Position - rigidbodyWrapper.Position).FlattenY().normalized
				: HeldDirection();
			if (aim == Vector3.zero)
			{
				return;
			}

			// The same ceiling, spent as an allowance per commit window: unburdened covers a full reversal within
			// it, burdened covers less - but standing still winding up is never a permanent lock.
			float rate = combatComponent.LungeTurnLimit / Mathf.Max(0.01f, combatSettings.LungeAimCommitTime);
			movementHandler.ForceRotation(aim, rate * control * delta / 180f);
		}

		/// <summary>
		/// Movement input in world space with its MAGNITUDE intact (capped at 1): a half-pushed stick commits half.
		/// </summary>
		private Vector3 HeldInput()
		{
			Vector3 input = movementHandler.InputSmooth;
			if (input.magnitude < movementHandler.MinimumInput)
			{
				return Vector3.zero;
			}

			Vector3 world = (Quaternion.LookRotation(movementHandler.InputAxis) * input).FlattenY();
			return Vector3.ClampMagnitude(world, 1f);
		}

		/// <summary>
		/// Direction being held at this instant, in world space; zero when nothing meaningful is pressed.
		/// </summary>
		private Vector3 HeldDirection()
		{
			Vector3 held = HeldInput();
			return held == Vector3.zero ? Vector3.zero : held.normalized;
		}

		/// <summary>
		/// Where this leap TRIES to go: the target when there is one, else the direction held, else straight on.
		/// </summary>
		private Vector3 LungeAim()
		{
			if (target != null && agentTargetable != null)
			{
				Vector3 to = (target.Position - rigidbodyWrapper.Position).FlattenY();
				if (to.sqrMagnitude > 0.0001f)
				{
					return to.normalized;
				}
			}

			Vector3 held = HeldDirection();
			return held != Vector3.zero ? held : rigidbodyWrapper.Forward.FlattenY().normalized;
		}

		/// <summary>
		/// 0-1 commitment bought for this swing. ONE rule, targeted or not: pushing toward where the leap is
		/// going IS the commitment, sprint buys the same thing without steering, and across or away buys nothing.
		/// Untargeted the leap goes where you push, so a full push commits fully.
		/// </summary>
		private float ResolveCommit(Vector3 aim)
		{
			// Magnitude INTACT, so the dot carries how hard you are pushing as well as which way.
			Vector3 held = HeldInput();
			float lean = held == Vector3.zero ? 0f : Mathf.Clamp01(Vector3.Dot(held, aim));
			return Mathf.Max(Mathf.Clamp01(combatComponent.LungeIntent), lean);
		}

		/// <summary>
		/// Clamps an aim to the widest angle load lets this lunge turn from the facing it launched at.
		/// </summary>
		private Vector3 CapTurn(Vector3 aim)
		{
			if (stickLaunchForward.sqrMagnitude < 0.0001f)
			{
				return aim;
			}

			float limit = combatComponent.LungeTurnLimit * Mathf.Deg2Rad;
			return Vector3.RotateTowards(stickLaunchForward, aim, limit, 0f).normalized;
		}

		/// <summary>
		/// Degrees per second the heading converges on its (already capped) aim: the turn spread across the leap's own
		/// airtime, plus Acuity TRACKING when there is a target, lerped toward homing by the charge.
		/// </summary>
		private float LeapTurnRate()
		{
			float rate = stickTurnSpeed;
			if (target != null)
			{
				rate += Mathf.Lerp(stickAimStat ?? 0f, 1f, StormCharge) * combatSettings.StickTurnRate;
			}

			return rate;
		}

		/// <summary>
		/// Charges Stamina for the metres beyond the free lunge, returning the cover the bar could actually buy.
		/// </summary>
		private float PayForLunge(float cover)
		{
			float free = combatComponent.ComputeStickRange(move, 0f) +
				combatComponent.ComputeStormRange(move, ChargeFraction);
			float paid = cover - free;
			if (paid <= 0f)
			{
				return cover;
			}

			ResourceStat stamina = statHandler.ResourceStats.E;
			float spent = stamina.Drain(combatComponent.ComputeLungeCost(paid));
			lungePaid = spent > 0f;
			statHandler.RewardExpPoints(Element.Air, spent, ExpSources.LUNGE);

			// A storm already grunted by its stretch; a plain lunge grunts by the stamina it bought.
			if (lungePaid && !hasStorm && stamina.Max > 0f)
			{
				agentAudioHandler.PlayExertion(Mathf.Clamp01(spent / stamina.Max));
			}

			// Back through the price at the RAW rate Drain was handed, so a Drain multiplier cannot skew the metres.
			float raw = spent / Mathf.Max(0.0001f, stamina.DrainMult);
			return free + Mathf.Min(paid, combatComponent.ComputeLungeMetres(raw));
		}

		/// <summary>
		/// Begins the leap — one mechanism for stick, storm and an untargeted swing. Drives a heading until arrival
		/// or until the budget is spent, then releases the held swing.
		/// </summary>
		private void BeginApproach()
		{
			Vector3 toTarget = target != null && agentTargetable != null
				? (target.Position - rigidbodyWrapper.Position).FlattenY()
				: Vector3.zero;
			float distance = toTarget.magnitude;
			bool aimed = distance > 0.0001f;

			// Same aim the commitment was priced against - one source, so distance paid and distance flown agree.
			Vector3 forward = rigidbodyWrapper.Forward.FlattenY().normalized;
			stickAim = LungeAim();
			// It STARTS along the current facing and turns in, and load CAPS that turn - a desired angle past the
			// ceiling is reached only as far as the ceiling goes.
			stickHeading = forward.sqrMagnitude < 0.0001f ? stickAim : forward;
			stickLaunchForward = stickHeading;
			stickAim = CapTurn(stickAim);

			if (stickAim.sqrMagnitude < 0.0001f || stickHeading.sqrMagnitude < 0.0001f)
			{
				ReleaseSwing();
				return;
			}

			// Land inside our own reach rather than at its very tip — StickBite deep, floored at the attack-pose
			// margin so the leap never aims into the target's body. Shared with the AI's strike timing.
			float desired = aimed ? combatComponent.ComputeStickDesired(move, target.Radius) : 0f;
			// No target, no gap to measure: the whole budget IS the leap, spent along the held direction.
			float gap = aimed ? distance - desired : maxStick;

			if (gap <= 0f)
			{
				// Already inside the bite: swing out at once, carrying a fraction of a full leap speed. It plants
				// exactly like a leap does — the two only differ in how they get moving, never in how they stop.
				ReleaseSwing();
				BeginInertia();
				return;
			}

			// Clamped, not gated: past the budget the leap saturates and falls short. The budget is the DRIVE;
			// planting the feet happens once it has been crossed, so the trajectory is StickRange + a short tail.
			float cover = PayForLunge(Mathf.Min(gap, maxStick));
			if (cover <= 0.0001f)
			{
				ReleaseSwing();
				BeginInertia();
				return;
			}

			// Leap speed is sized off stick range, not the storm's extended cover, so a long storm isn't handed an
			// absurd speed. Max: a high Stick_Range leap can outrun StormSpeed, and a storm must never close slower.
			float leapSpeed = LeapSpeed(Mathf.Min(cover, combatComponent.ComputeStickRange(move)));
			stickSpeed = hasStorm
				? Mathf.Lerp(leapSpeed,
					Mathf.Max(combatSettings.StormSpeed * (stormSpeedStat ?? 1f), leapSpeed),
					StormCharge)
				: leapSpeed;
			stickSpeed = Mathf.Max(stickSpeed, 0.01f);
			stickDesired = desired;
			stickLastDistance = distance;
			// The gap a Lunging region plays across. Progress is measured against THIS, not the clamped
			// cover, so a leap that falls short still reads as partway rather than complete.
			stickGap = gap;
			stickRemaining = gap;
			// Seeded from our REAL speed: the intended leap speed would count as the target walking in.
			stickClosingRate = aimed
				? Vector3.Dot(rigidbodyWrapper.PredictedVelocity.FlattenY(), toTarget / distance) : 0f;
			// THE range cap: sizing speed to 'cover' doesn't bound distance, so meter real ground against a budget.
			stickBudget = cover;
			stickTravelled = 0f;
			stickLastPosition = rigidbodyWrapper.Position;
			// Backstop; arrival and exhaustion both release sooner.
			stickTravelTimer = cover / stickSpeed * travelTimeoutSlack;
			stickDriveStep = -1;
			stickTravelling = true;
			// Spread the whole turn across the leap's own airtime - the CAP is the stat, the pacing is just smoothing.
			stickTurnSpeed = Vector3.Angle(stickHeading, stickAim) / Mathf.Max(0.01f, LeapTime(cover));


			movementHandler.AutoUpdateMovement = false;

			// The swing is HELD across every approach, targeted or not - Paused freezes RunTime, so the wind-up
			// pose holds and the hit-detection delay is never spent in transit. ONE path: the pose, the release
			// and the timeline cannot diverge between the two cases.
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

			// Distance still to cover, in ONE place for both cases: the budget remainder, tightened by the real
			// gap when there is a target. Never updated inside a branch, or the pose reads a stale leap.
			stickRemaining = Mathf.Max(0f, stickBudget - stickTravelled);

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

					// A live target keeps moving, so the aim follows it - still bounded by the same turn ceiling.
					stickAim = CapTurn(toTargetDir);

					// Release so the body, braked by the plant it lands in, reaches the bite as the blade reaches contact.
					float lead = TimeUntilRunTime(move.ContactTime);
					float remaining = distance - stickDesired;
					stickRemaining = Mathf.Min(stickRemaining, Mathf.Max(0f, remaining));

					// Only our share of the closing brakes; the target's share keeps coming at its own pace.
					float ownSpeed = Mathf.Max(0f, Vector3.Dot(rigidbodyWrapper.PredictedVelocity.FlattenY(), toTargetDir));
					float covered = PredictPlantTravel(ownSpeed, lead) + (stickClosingRate - ownSpeed) * lead;

					// A stalled or retreating gap never gets covered — budget and timeout own those cases.
					release = remaining <= 0f || remaining <= covered;
				}
			}

			if (release)
			{
				EndApproach();
				return;
			}

			// ONE rate for heading and body: the heading is what is limited, so the body may follow it exactly.
			stickHeading = Vector3.RotateTowards(
				stickHeading, stickAim, LeapTurnRate() * Mathf.Deg2Rad * delta, 0f).normalized;

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
			ReleaseSwing();

			// Brake the LEAP immediately - a swing that hangs before releasing must not drift onward at leap
			// speed while it waits. The swing's own lurch then arrives later and plants itself in turn.
			BeginPlant(stickSpeed);
			BeginInertia();
		}

		/// <summary>
		/// The forward lurch the swing itself gives the body. Held back by InertiaDelay so it lands on the frame
		/// the animation throws its weight, rather than the instant the swing was released.
		/// </summary>
		private void BeginInertia()
		{
			// A PUSH, not an add: it cannot stack, so a swing following a fast leap is simply a no-op rather
			// than launching the agent. That is why the same value is used whether or not there was a leap.
			pendingInertia = LurchSpeed;

			if (move.InertiaDelay <= 0f)
			{
				ApplyInertia();
				return;
			}

			inertiaPending = true;
		}

		private void ApplyInertia()
		{
			rigidbodyWrapper.Push(stickHeading * pendingInertia);

			// Brake the lurch too. The leap already got its own plant on landing; without this second one a
			// staggered swing would coast on whichever push came last.
			BeginPlant(pendingInertia);
		}

		/// <summary>
		/// Starts the plant. <paramref name="entrySpeed"/> is the INTENDED speed — an impulse only resolves on the
		/// next physics step, so the rigidbody cannot be asked how fast it is about to be going.
		/// </summary>
		private void BeginPlant(float entrySpeed)
		{
			stickBrakeLength = PlantLength;

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
			stickSwung = false;
			lungePaid = false;

			// Storm is the same leap with a charge-extended budget — charge buys reach on top of the stick.
			float storm = combatComponent.ComputeStormRange(move, ChargeFraction);
			hasStorm = storm > 0f;
			// ABSOLUTE maximum reach of this swing, leap included — the acquisition horizon. Taken from the combat
			// authority so it cannot drift from the reach the AI fire gate uses. Always the FULL lunge: acquisition
			// happens before commitment is known, and an under-committed leap simply falls short.
			maxReach = combatComponent.ComputeEffectiveReach(move) + storm;

			// TARGETING: a hard lock always wins, otherwise acquire whoever the swing is aimed at. Must run
			// BEFORE TargetVelocity is overwritten below - that vector IS the held direction.
			target = targeter.Target ?? AcquireStickTarget();

			// Commitment settles HERE, against the direction the leap will actually take.
			lungeCommit = ResolveCommit(LungeAim());
			maxStick = combatComponent.ComputeStickRange(move, lungeCommit) + storm;

			if (target != null)
			{
				rigidbodyWrapper.TargetVelocity = (target.Position - RigidbodyWrapper.Position).normalized;
			}

			if (hasStorm && Agent.Identification.HasAll(EntityLabels.PLAYER))
			{
				// Scaled by the charge so a light overcharge stays subtle and a full one screams.
				stormShake = new ContinuousShakeSource(
					stormShakeMagnitude * StormCharge,
					-rigidbodyWrapper.TargetVelocity);
				agentImpactHandler.ReportImpact(new ImpactData
				{
					Source = Agent,
					Direction = -rigidbodyWrapper.TargetVelocity,
					Location = Agent.Transform.position,
					ShakeSource = stormShake
				});
			}

			// The charge rumble hands over to a kick: along the storm if there is one, else undirected.
			chargeShake?.Dispose();
			chargeShake = null;
			// Undirected, X/Y would be a constant camera lean rather than a push; a zero magnitude means the default.
			Vector3 kick = hasStorm ? -rigidbodyWrapper.TargetVelocity : Vector3.zero;
			Vector3 kickMagnitude = hasStorm ? releaseShakeMagnitude : new Vector3(0f, 0f, releaseShakeMagnitude.z);
			if (ChargeSpent > 0f && kickMagnitude != Vector3.zero && Agent.Identification.HasAll(EntityLabels.PLAYER))
			{
				releaseShake?.Dispose();
				releaseShake = new ContinuousShakeSource(kickMagnitude, kick, intensity: ChargeSpent);
				releaseShakeTimer = 0f;
				agentImpactHandler.ReportImpact(new ImpactData
				{
					Source = Agent,
					Direction = kick,
					Location = Agent.Transform.position,
					ShakeSource = releaseShake
				});
			}
			agentAudioHandler.StopStrain(this);

			// A storm launch is its own effort, voiced by the stretch; a plain lunge grunts by its stamina instead.
			if (hasStorm)
			{
				agentAudioHandler.PlayExertion(ChargeSpent);
			}

			// Closing the gap starts the moment the swing is released. InertiaDelay used to gate this, but it
			// marks where the swing's own momentum carries the body - which is after the approach, not before.
			BeginApproach();
		}

		/// <summary>Fires the swing once per performance; the untargeted leap releases before the drive ends.</summary>
		private void ReleaseSwing()
		{
			if (stickSwung)
			{
				return;
			}

			stickSwung = true;
			OnSwing();
		}

		private void OnSwing()
		{
			// Energy: priced on LIMB mass by the combat authority, not the strike's mass — what a swing costs the body
			// is what it wields. Paid at the swing, not the release, so a cancelled lunge or storm costs none.
			float drained = statHandler.ResourceStats.N.Drain(combatComponent.ComputePerformCost(move));
			agentAudioHandler.PlayExertion(Mathf.Clamp01(drained / statHandler.ResourceStats.N.Reserve));

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

		/// <summary>Player camera rumble that grows with the pool spent, holding once it runs dry.</summary>
		private void UpdateChargeShake()
		{
			// A zero magnitude would fall back to the shaker's default, so none is none.
			if (ChargeSpent <= 0f || chargeShakeStrength <= 0f)
			{
				return;
			}

			if (chargeShake == null)
			{
				if (!Agent.Identification.HasAll(EntityLabels.PLAYER))
				{
					return;
				}

				// Undirected, so Z only: X/Y would lean the camera instead of shaking it.
				chargeShake = new ContinuousShakeSource(
					new Vector3(0f, 0f, chargeShakeStrength), Vector3.zero, intensity: 0f);
				agentImpactHandler.ReportImpact(new ImpactData
				{
					Source = Agent,
					Location = Agent.Transform.position,
					ShakeSource = chargeShake
				});
			}

			chargeShake.Intensity = ChargeSpent;
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

					// BODY momentum only. The strike's force is the receiver's second channel — it needs their
					// coupling and endurance wear to size it. Predicted, so the plant's queued braking counts.
					Vector3 inertia = rigidbodyWrapper.PredictedVelocity;

					// Per-axis base output (x=Slash, y=Power, z=Pierce) from AgentCombatComponent.
					// Runtime modifiers (charge, carry, malice) are applied below. Weight costs speed, never power.
					AgentCombatComponent.MoveOutput moveOutput = combatComponent.GetMoveOutput(move);
					Vector3 baseOutput = moveOutput.Output;

					// Reaching past its contact point, a strike only lands what its body's momentum still carries.
					float carry = MomentumCarry();
					float powerScale = totalCharge * carry;
					float powerValue = baseOutput.y * powerScale;

					// Both bands take the same runtime scaling as Power.
					float powerBand = moveOutput.PowerBand * powerScale;
					float forceBand = moveOutput.ForceBand * powerScale;

					// Assemble the offence vector's runtime-modified channels before Malice.
					float slashValue = baseOutput.x * carry;
					float pierceValue = (baseOutput.z + chargePoints * combatSettings.ChargePiercePerPoint) * carry;

					// MALICE: spite scales the WHOLE offence vector. Coverage = the fraction the pool pays for
					// (Drain applies the Hostility-driven DrainMult). Symmetrical to Grace.
					float totalOffence = slashValue + powerValue + pierceValue;
					float maliceMult = 1f;
					float maliceDrained = 0f;
					if (totalOffence > 0f)
					{
						maliceDrained = statHandler.ResourceStats.NW.Drain(totalOffence, true);
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
						combatComponent.ComputeLimbMass(move),
						move.BodyMassFraction,
						combatComponent.Rank,
						finalSlash,
						finalPower,
						finalPierce,
						powerBand * maliceMult,
						forceBand * maliceMult,
						luckStat,
						baseStrengthSpeedFactor * appliedPhaseMult
					);

					// What swung it, so both lanes can voice the armament's material. Unarmed writes nothing.
					string weaponSurface = combatComponent.GetMoveSurface(move);
					if (!string.IsNullOrEmpty(weaponSurface))
					{
						hitData.Data.SetValue(HitDataIdentifiers.WEAPON_SURFACE, weaponSurface);
					}
					hitData.Data.SetValue(HitDataIdentifiers.CARRY, carry);

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
			// Landing costs Energy on top of the swing: resistance is what tires, so a braced target costs most.
			// Clamped to what is in the bar — only a swing you chose to throw may overdraw into the reserve.
			float hitCost = combatSettings.HitCost(hitData.Data.GetValue<float>(HitDataIdentifiers.FORCE));
			if (hitCost > 0f)
			{
				ResourceStat energy = statHandler.ResourceStats.N;
				float drainMult = Mathf.Max(0.0001f, energy.DrainMult);
				float affordable = Mathf.Max(0f, energy.Current) / drainMult;
				energy.Drain(Mathf.Min(hitCost, affordable));
			}

			float healthMax = hitData.Data.GetValue<float>(HitDataIdentifiers.HEALTH_MAX);
			if (healthMax <= 0f)
			{
				return;
			}

			statHandler.RewardExp(Element.Fire,
				hitData.Data.GetValue<float>(HitDataIdentifiers.BLUNT_DAMAGE) / healthMax, ExpSources.POWER_OUTPUT);
			statHandler.RewardExp(Element.Light,
				(hitData.Data.GetValue<float>(HitDataIdentifiers.PIERCE_DAMAGE) +
				hitData.Data.GetValue<float>(HitDataIdentifiers.CRIT_DAMAGE)) / healthMax, ExpSources.PIERCE_OUTPUT);
			statHandler.RewardExp(Element.Void,
				hitData.Data.GetValue<float>(HitDataIdentifiers.SLASH_DAMAGE) / healthMax, ExpSources.SLASH_OUTPUT);

			// A parried hit still connected, so the charge it delivered is still paid for.
			bool neglected = hitData.Data.GetValue<bool>(HitDataIdentifiers.BLOCKED);

			// A charge is worthless until it connects; pay once per swing for the charge that was delivered.
			if (!chargeRewarded && rawChargeSpent > 0f && !neglected)
			{
				chargeRewarded = true;
				statHandler.RewardExpPoints(Element.Light, rawChargeSpent, ExpSources.STATIC_HIT);
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

				if (hitData.Data.GetValue<bool>(HitDataIdentifiers.BLOCKED))
				{
					Performer.TryCancel(true);
					rigidbodyWrapper.ResetVelocity();
					stunHandler.EnterStun(hitData, combatSettings.BlockedStunTime);
				}
				else
				{
					// Hit landed, or was parried — a parry means nothing to the attacker, so they bank Static either way.
					// MIRROR of Trauma (NW): they build spite off the health they LOSE, we build charge off the
					// health we TAKE — stolen vitality. A crit needs no branch; its damage is already in here.
					float dealt = hitData.Data.GetValue<float>(HitDataIdentifiers.DAMAGE_DEALT);
					if (chargeStat != null && dealt > 0f)
					{
						chargeStat.BaseValue += dealt * combatSettings.StaticPerDamage;
					}
				}

				// A parry hands us the half of the stagger it negated. No stun; it just opens us to a punish.
				float enduranceReturn = hitData.Data.GetValue<float>(HitDataIdentifiers.ENDURANCE_RETURN);
				if (enduranceReturn > 0f)
				{
					statHandler.ResourceStats.W.Drain(enduranceReturn);
				}

				// Our share of the strike's force, bounced back off the target's footing (computed receiver-side).
				// Applied after the outcome so a block or parry reset can't swallow the bounce.
				rigidbodyWrapper.Push(hitData.Data.GetValue(HitDataIdentifiers.INERTIA_BRAKE, Vector3.zero));

				// Scales with impact; crits pause for a fixed beat, and a parry pulls it toward its perfect-parry
				// pause by how well it was timed, so a clean one reads as a longer freeze.
				float impact = hitData.Data.GetValue<float>(HitDataIdentifiers.IMPACT);
				float normalPause = combatSettings.HitPauseSender.Lerp(
					impact * hitData.Data.GetValue(HitDataIdentifiers.CARRY, 1f) * (1f / performSpeedStat.Value));
				float hitPause = hitData.Data.GetValue<bool>(HitDataIdentifiers.PARRIED)
					? Mathf.Lerp(normalPause, combatSettings.ParriedHitPause,
						Mathf.Clamp01(hitData.Data.GetValue<float>(HitDataIdentifiers.PARRY_QUALITY)))
					: hitData.Data.GetValue<bool>(HitDataIdentifiers.CRIT) ? combatSettings.CritSenderHitPause
					: normalPause;

				float remainingPause = hitPauseMod == null ? 0f : Mathf.Max(0f, hitPauseMod.Timer.Remaining);
				if (hitPauseMod == null || hitPause > remainingPause)
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
				else
				{
					// A longer pause is already running; the tail waits for that one to lift instead.
					hitPause = remainingPause;
				}

				hitData.Data.SetValue(HitDataIdentifiers.HIT_PAUSE, hitPause);

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
