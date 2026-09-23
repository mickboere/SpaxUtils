using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(CombatSettings), menuName = "ScriptableObjects/Combat/" + nameof(CombatSettings))]
	public class CombatSettings : ScriptableObject, IService
	{
		// Lightest mass any strike is priced at, so limbless strikes still cost something.
		private const float EXERTION_FLOOR_MASS = 1f;

		public Vector2 HitPauseReceiver => hitPauseReceiver;
		public Vector2 HitPauseSender => hitPauseSender;
		public AnimationCurve HitPauseCurve => hitPauseCurve;
		public float MinStunTime => minStunTime;
		public float BlockedStunTime => blockedStunTime;
		public float ParrierHitPause => parrierHitPause;
		public float ParriedHitPause => parriedHitPause;
		public float CritSenderHitPause => critSenderHitPause;
		public float CritReceiverHitPause => critReceiverHitPause;
		public float StaticPerDamage => staticPerDamage;
		public float MaliceGain => maliceGain;
		public float ChargePowerPerPoint => chargePowerPerPoint;
		public float ChargePiercePerPoint => chargePiercePerPoint;
		public float ChargeEfficiencyDecay => chargeEfficiencyDecay;
		public float ChargeEmptyGrace => chargeEmptyGrace;
		public float ChargeBalance => chargeBalance;
		public float PerformBalance => performBalance;
		public float Restitution => restitution;
		public float MeleeFloorBite => meleeFloorBite;
		public float StickRange => stickRange;
		public Vector2 StickRangeThrustScale => stickRangeThrustScale;
		public float StickTime => stickTime;
		public float StickBite => stickBite;
		public float StickPlant => stickPlant;
		public float StickIdleInertia => stickIdleInertia;
		public float StickTurnRate => stickTurnRate;
		public float StickFreeFraction => stickFreeFraction;
		public float StickCostPerMetre => stickCostPerMetre;
		public float StickCostReferenceMass => stickCostReferenceMass;
		public float LungeTurnLoadFactor => lungeTurnLoadFactor;
		public float LungeTurnLoadExponent => lungeTurnLoadExponent;
		public float LungeAimCommitTime => lungeAimCommitTime;
		public float StickAcquireAngle => stickAcquireAngle;
		public float StormRange => stormRange;
		public float StormMinCharge => stormMinCharge;
		public float StormSpeed => stormSpeed;
		public AnimationCurve RearExposureCurve => rearExposureCurve;
		public float EnergyPerForce => energyPerForce;
		public float CritPivot => critPivot;
		public float CritMultiplier => critMultiplier;
		public float StaggerDamageWeight => staggerDamageWeight;
		public float BluntScale => bluntScale;
		public float BluntWallExponent => bluntWallExponent;
		public float ContestPower => contestPower;

		[Header("Hit Pause Settings")]
		[SerializeField, MinMaxRange(0f, 1f)] private Vector2 hitPauseReceiver = new Vector2(0.05f, 0.75f);
		[SerializeField, MinMaxRange(0f, 1f)] private Vector2 hitPauseSender = new Vector2(0.05f, 0.25f);
		[SerializeField] private AnimationCurve hitPauseCurve;
		[SerializeField, Tooltip("Minimum duration (s) of a stun from depleted endurance.")]
		private float minStunTime = 0.5f;
		[SerializeField] private float blockedStunTime = 1.25f;
		[SerializeField, Tooltip("Fixed hit-pause (s) for the agent who parried. Shorter than the attacker's, so recovering first is the reward.")]
		private float parrierHitPause = 0.5f;
		[SerializeField, Tooltip("Fixed hit-pause (s) for the attacker whose blow was parried. Ignores impact.")]
		private float parriedHitPause = 1f;
		[SerializeField, Tooltip("Fixed hit-pause (s) for the attacker who landed a crit. Ignores impact.")]
		private float critSenderHitPause = 0.5f;
		[SerializeField, Tooltip("Fixed hit-pause (s) for the agent who was critted. Ignores impact.")]
		private float critReceiverHitPause = 1f;

		// Tuned in SpecGraph (Tools/Graphs/damage.model.json); keep the two in step.
		[Header("Damage")]
		[SerializeField, Min(0f), Tooltip("How many extra times over Yield walls a point, proportionally. Higher = weaker pierce and rarer crits.")]
		private float critPivot = 3.5f;
		[SerializeField, Min(0f), Tooltip("A crit adds this many times the pierce hit it came from, sized as if unguarded.")]
		private float critMultiplier = 3f;
		[SerializeField, Min(0f), Tooltip("How much damage wears endurance down, next to force which always counts in full. Lower = power staggers comparatively harder.")]
		private float staggerDamageWeight = 1f;
		[SerializeField, Min(0f), Tooltip("Multiplies blunt offence: Power's level, leaving its flat quality scaling alone.")]
		private float bluntScale = 1.2f;
		[SerializeField, Min(0.01f), Tooltip("Steepness of blunt against its wall. Higher = weaker when outranked, crushing when outranking.")]
		private float bluntWallExponent = 3f;
		[SerializeField, Range(0.05f, 1f), Tooltip("How hard each channel's two contests compound. Below 1 a lopsided match hits less extreme; an even match is unchanged.")]
		private float contestPower = 0.4f;
		[SerializeField, Min(0f), Tooltip("Energy drained per point of resolved force when a strike lands: what the defender refused is what tires the arm that swung it.")]
		private float energyPerForce = 1f;

		[Header("Force")]
		[SerializeField, Range(0f, 2f), Tooltip("How hard excess mass scales force. 0 = mass ignored, 1 = proportional to how much heavier than its rank expects.")]
		private float forceMassExponent = 1f;

		[Header("Static / Charge Economy")]
		// CLOSED LEDGER: every point of offence becomes Static for someone — the attacker for what got
		// through, the defender for what did not. Damage type is irrelevant to it.
		[SerializeField, Min(0f), Tooltip("Static built per point of damage landed (attacker) or stopped (defender). 1 = an offence point is always exactly one Static, banked by whoever won it.")]
		private float staticPerDamage = 1f;
		// Tuned in SpecGraph (Tools/Graphs/charge.json). There is no charge cap: draining the pool IS the cap.
		[SerializeField, Tooltip("Charge multiplier gained per point STORED (post-efficiency). 0.02 = 50 points → +1× power.")]
		private float chargePowerPerPoint = 0.02f;
		[SerializeField, Range(0f, 1f), Tooltip("Pierce offence added per point STORED. The charge's second payout, beside the power multiplier.")]
		private float chargePiercePerPoint = 0.25f;
		[SerializeField, Min(0.01f), Tooltip("Fraction of the Static pool spent per HALVING of charge efficiency. Lower = the charge goes wasteful sooner.")]
		private float chargeEfficiencyDecay = 0.3f;
		[SerializeField, Min(0f), Tooltip("Seconds the warning loops with the pool empty before the attack auto-releases.")]
		private float chargeEmptyGrace = 0.75f;

		[Header("Malice")]
		[SerializeField, Min(0f), Tooltip("Malice (NW) built per unit of INCOMING offence (Slash+Power+Pierce), regardless of what the hit ended up dealing. Same basis Malice is spent against, so the ledger is symmetric. The Hostility→Gain mapping remains the per-agent dial; this is the global rate.")]
		private float maliceGain = 1f;

		[Header("Balance")]
		[SerializeField, Range(0.01f, 1f), Tooltip("How much balance is maintained while charging a melee move. Universal across melee moves.")]
		private float chargeBalance = 1f;
		[SerializeField, Range(0.01f, 1f), Tooltip("How much balance is maintained while performing a melee move. Universal across melee moves.")]
		private float performBalance = 1f;

		[Header("Physics")]
		[SerializeField, Range(0f, 1f), Tooltip("Elasticity of the inertia-sharing clash when a hit lands. 0 = perfectly inelastic (both bodies hold the gap), 1 = fully elastic (they bounce apart). Scales both the receiver's knockback and the hitter's self-brake by (1 + restitution).")]
		private float restitution = 0f;
		[SerializeField, Range(0f, 1f), Tooltip("HARD depth limit while attacking, read like StickBite: 0 = never closer than the tip of reach, 1 = until the bodies touch. Keep ABOVE StickBite or the floor blocks the leap short of its aim.")]
		private float meleeFloorBite = 0.75f;

		// STICK: DISTANCE IS THE CONFIGURED UNIT. StickRange decides how far; StickTime is the only speed-domain
		// knob, and it is a derivation rule rather than a target. Nothing here touches knockback.
		[Header("Sticky Combat")]
		[SerializeField, Min(0f), Tooltip("BASE distance (metres) an attack can close by leaping, at Stick_Range = 1 and no load. The Agility-fed Stick_Range stat multiplies it (~1x at level 0 up to ~6x at 100), the move's thrust and equip LoadPenalty cut it back down.")]
		private float stickRange = 1.5f;
		[SerializeField, MinMaxRange(0f, 1f, true), Tooltip("Fraction of the stick range granted by the move's THRUST (StrikeDirection.z, forward half only): X at zero thrust (a pure sweep still closes, just reluctantly), Y at a full lunge.")]
		private Vector2 stickRangeThrustScale = new Vector2(0.6f, 1f);
		[SerializeField, Min(0.01f), Tooltip("Airtime (seconds) of a FULL-range leap — the ONLY speed knob, every leap speed derives from it. Shorter leaps scale by sqrt(gap / range), the real jump relationship. NOTE: top leap speed is (stick range / this), so it rises with Stick_Range — a maxed-Agility leap is both longer AND much faster.")]
		private float stickTime = 0.25f;
		[SerializeField, Range(0f, 1f), Tooltip("How deep into its own reach a leap lands. 0 = stop at the very tip of reach (fragile — the target only just gets clipped), 1 = close all the way to the separation floor. The bite absorbs the target's own movement during the swing, so keep it off 0.")]
		private float stickBite = 0.35f;
		[SerializeField, Range(0f, 2f), Tooltip("Drag length of the plant after a swing's forward motion, in StickRanges. Braking is QUADRATIC (a = v^2 / L), so a fast lunge is killed hard while a slow step is barely touched — that difference is the point, and this only sets the overall scale. Higher = longer, floatier slide; lower = snappier. 0 hands straight back to normal movement.")]
		private float stickPlant = 0.25f;
		[SerializeField, Range(0f, 1f), Tooltip("Fraction of a FULL leap's speed carried by a swing that has nothing to leap at — no target acquired, or already inside the bite. The swing comes out immediately (no travel, no hold) but still steps forward, just less than a real leap. 0 = motionless.")]
		private float stickIdleInertia = 0.5f;
		[SerializeField, Min(0f), Tooltip("Degrees per second a leap can re-aim its heading at Stick_Aim = 1 (Acuity-fed). The correction only ROTATES the leap, never lengthens it, so it cannot rescue an out-of-range read — that is Storm's job.")]
		private float stickTurnRate = 180f;
		[SerializeField, Range(0f, 180f), Tooltip("Half-angle of the acquisition cone around the held movement direction. An enemy outside it is never leapt at, so a deliberate swing away from someone stays a swing away.")]
		private float stickAcquireAngle = 60f;
		[SerializeField, Range(0f, 1f), Tooltip("Fraction of StickRange every attack lunges for FREE. Committing past it (a held direction, or the sprint button while targeted) is what costs Stamina, so a lunge is never gated - only its top half is bought.")]
		private float stickFreeFraction = 0.5f;
		[SerializeField, Min(0f), Tooltip("Stamina a single PAID lunge metre costs an agent of Reference Mass. Only the distance beyond StickFreeFraction is charged; a bar too low simply buys fewer metres.")]
		private float stickCostPerMetre = 12f;
		[SerializeField, Min(0.0001f), Tooltip("Mass (kg) the lunge cost is quoted at. Heavier bodies pay proportionally more, same convention as the jump.")]
		private float stickCostReferenceMass = 100f;
		[SerializeField, Min(0f), Tooltip("How hard carried load caps the angle a lunge may turn in: 180 degrees / (1 + this * (load/capacity)^exponent). At 1 an agent loaded exactly to capacity can only turn 90 degrees, and less beyond it. Unburdened is always the full 180.")]
		private float lungeTurnLoadFactor = 1f;
		[SerializeField, Min(0.01f), Tooltip("Shapes the load curve on the turn rate. Above 1 keeps light loads nearly free and makes the penalty bite near capacity.")]
		private float lungeTurnLoadExponent = 2f;
		[SerializeField, Min(0.01f), Tooltip("Seconds of charge after which the aim is fully committed and can no longer be steered. Lets you pick a direction on the press, release the stick, and still land the free lunge.")]
		private float lungeAimCommitTime = 0.4f;

		// STORM: the charged upgrade to a stick. Extends the same leap and homes instead of committing to a
		// heading; both terms scale with the charge fraction, reaching full only on a pool-deep charge.
		[Header("Storming")]
		[SerializeField, Min(0f), Tooltip("Extra distance (metres) a FULLY charged storm adds on top of StickRange. Scaled by charge past StormMinCharge (nothing below it) and by the same StickRangeThrustScale lane as the stick, so a sweep storms less far than a thrust.")]
		private float stormRange = 6f;
		[SerializeField, Range(0f, 1f), Tooltip("Charge fraction a storm must EXCEED to exist at all. Releasing a plain attack always banks a few points on its way out, so without this deadzone every swing registered as a hair of storm - trail, held swing and all. Past it the storm ramps up from zero, so there is no jump.")]
		private float stormMinCharge = 0.1f;
		[SerializeField, Min(0f), Tooltip("Speed (m/s) of a FULLY charged storm at Storm_Speed = 1; the Acuity-fed stat multiplies it. A partial charge lerps up from the ordinary leap speed, and a fast leap floors it, so a storm never closes slower than the lunge it upgrades.")]
		private float stormSpeed = 15f;

		[Header("Vulnerability")]
		[SerializeField, Tooltip("Maps how exposed the receiver is to a hit based on the angle it lands from, into a 0..1 rear-exposure factor that lerps the receiver's Vulnerability toward 1 (full crit). INPUT (X, 0..1): the hit's angle relative to the receiver's facing - 0 = struck dead-on from the front, 0.5 = struck from the side, 1 = struck from directly behind. OUTPUT (Y, 0..1): exposure - 0 = no added vulnerability (use the receiver's base Vulnerability stat), 1 = fully exposed (Vulnerability forced to 1, guaranteeing a crit if the hit couples). Default shape: front/sides approx 0, ramping up to 1 at the rear.")]
		private AnimationCurve rearExposureCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 0f), new Keyframe(1f, 1f));

		[Header("Wielding")]
		[SerializeField, MinMaxRange(0.5f, 1.5f), Tooltip("Swing-speed multiplier by wield ratio (strength / limb+weapon mass): X = factor when badly under-strength (heavy weapon → slow), Y = max over-strength bonus. UNIVERSAL across all melee moves — the per-move difference comes from limb/weapon mass, not this curve; a natural strike (kick) has no limb mass so it reads ratio 1 → factor 1. Read by the move performer AND by AI move-selection / strike-timing so a heavy swing is anticipated as the slow swing it is.")]
		private Vector2 strengthSpeedModRange = new Vector2(0.5f, 1.15f);
		[SerializeField, Range(1f, 4f), Tooltip("Exponent for the under-strength speed curve. Higher = slower ramp-up before reaching mass-equal strength.")]
		private float speedCurveExponent = 2f;
		[SerializeField, Min(1f), Tooltip("Wield ratio (strength / limb mass) at which the over-strength speed bonus reaches strengthSpeedModRange.y. E.g. 10 = need 10x the limb mass in strength.")]
		private float overStrengthFullRatio = 10f;
		[SerializeField, Range(0f, 1f), Tooltip("Power multiplier when badly under-strength (wield ratio 0), rising to 1 at a mass-equal wield. Universal, for the same reason the speed curve is: the per-move difference is mass.")]
		private float minWieldPowerFactor = 0.4f;
		[SerializeField, Min(1f), Tooltip("Tenacity levels a weapon may outweigh your Strength by before the wield penalty is full. Weapon mass only — your own arm never counts against you.")]
		private float wieldFullPenaltyLevels = 20f;

		/// <summary>
		/// Swing speed for a wielder of <paramref name="strength"/> holding <paramref name="weaponMass"/>: slower the more
		/// Tenacity levels short it is, faster when strength outstrips it. Shared by performer, selection and AI timing.
		/// </summary>
		public float WieldSpeedFactor(float strength, float weaponMass)
		{
			float shortfall = WieldShortfall(strength, weaponMass);
			if (shortfall > 0f)
			{
				return Mathf.Lerp(1f, strengthSpeedModRange.x,
					Mathf.Pow(shortfall, 1f / Mathf.Max(0.01f, speedCurveExponent)));
			}
			float ratio = weaponMass <= 0f ? overStrengthFullRatio : strength / weaponMass;
			float extra = Mathf.Clamp01((ratio - 1f) / Mathf.Max(0.01f, overStrengthFullRatio - 1f));
			return Mathf.Lerp(1f, strengthSpeedModRange.y, extra);
		}

		/// <summary>
		/// How far past the wielder a weapon weighs, as 0..1 over <see cref="wieldFullPenaltyLevels"/> Tenacity levels.
		/// 0 whenever Strength covers the weapon; distance, so a 5kg blade is as heavy at rank 1 as at rank 100.
		/// </summary>
		public float WieldShortfall(float strength, float weaponMass)
		{
			float over = weaponMass - strength;
			if (over <= 0f)
			{
				return 0f;
			}
			float levels = over / SpaxFormulas.WEAPON_MASS_PER_RANK;
			return Mathf.Clamp01(levels / Mathf.Max(1f, wieldFullPenaltyLevels));
		}

		/// <summary>
		/// Universal wield POWER factor: a swing too heavy for its wielder lands softer, down to
		/// <c>minWieldPowerFactor</c>. No over-strength bonus — extra strength buys speed, not output.
		/// </summary>
		public float WieldPowerFactor(float strength, float weaponMass)
		{
			return Mathf.Lerp(1f, minWieldPowerFactor, WieldShortfall(strength, weaponMass));
		}

		/// <summary>
		/// Exertion factor: how heavy this strike is against the mass its rank expects, so the authored cost
		/// stays a share of the pool. One source of truth: performer, selection and the AI affordability gate.
		/// </summary>
		public float ExertionFactor(float mass, float referenceMass)
		{
			return Mathf.Max(mass, EXERTION_FLOOR_MASS) / Mathf.Max(referenceMass, 0.01f);
		}

		/// <summary>
		/// Energy a landed strike costs on top of its swing: resolved <paramref name="force"/> priced by
		/// <see cref="EnergyPerForce"/>. Resistance is what tires you, so a braced target costs the most.
		/// </summary>
		public float HitCost(float force)
		{
			return Mathf.Max(0f, force) * energyPerForce;
		}

		/// <summary>
		/// Mass the strike's effort moves vs what its rank swings, never below 1: the limb, blended toward the body by
		/// the share its legs LIFT. Forward body mass rides the hit's momentum knockback instead.
		/// </summary>
		public float ForceMassFactor(float limbMass, float hitterMass, float liftedBodyShare, float rank)
		{
			// One scale for both, so committing the body adds its mass instead of trading one excess for another.
			float expected = SpaxFormulas.ExpectedLimbMass(rank);
			float limb = Mathf.Pow(Mathf.Max(1f, limbMass / expected), forceMassExponent);
			float body = Mathf.Pow(
				Mathf.Max(1f, hitterMass * SpaxFormulas.BODY_DRIVE_RATIO / expected), forceMassExponent);
			return Mathf.Lerp(limb, body, Mathf.Clamp01(liftedBodyShare));
		}
	}
}
