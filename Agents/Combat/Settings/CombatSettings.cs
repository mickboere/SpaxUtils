using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(CombatSettings), menuName = "ScriptableObjects/Combat/" + nameof(CombatSettings))]
	public class CombatSettings : ScriptableObject, IService
	{
		public Vector2 HitPauseReceiver => hitPauseReceiver;
		public Vector2 HitPauseSender => hitPauseSender;
		public AnimationCurve HitPauseCurve => hitPauseCurve;
		public float BlockedStunTime => blockedStunTime;
		public float ParriedStunTime => parriedStunTime;
		public float DeflectorHitPause => deflectorHitPause;
		public float DeflectedHitPause => deflectedHitPause;
		public float DeflectEnduranceShare => deflectEnduranceShare;
		public float StaticGain => staticGain;
		public float MaliceGain => maliceGain;
		public float DeflectStaticPercent => deflectStaticPercent;
		public float BlockStaticPercent => blockStaticPercent;
		public float HitStaticPercent => hitStaticPercent;
		public float CritStaticPercent => critStaticPercent;
		public float ChargeConversionRatio => chargeConversionRatio;
		public float MaxChargeMultiplier => maxChargeMultiplier;
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
		public float StickAcquireAngle => stickAcquireAngle;
		public float StormRange => stormRange;
		public float StormSpeed => stormSpeed;
		public AnimationCurve RearExposureCurve => rearExposureCurve;
		public float ExertionCostAtRef => exertionCostAtRef;
		public float ExertionRefMass => exertionRefMass;
		public float ExertionRefBodyMass => exertionRefBodyMass;
		public float ExertionMassExponent => exertionMassExponent;
		public float ExertionFloorMass => exertionFloorMass;

		[Header("Hit Pause Settings")]
		[SerializeField, MinMaxRange(0f, 1f)] private Vector2 hitPauseReceiver = new Vector2(0.05f, 0.75f);
		[SerializeField, MinMaxRange(0f, 1f)] private Vector2 hitPauseSender = new Vector2(0.05f, 0.25f);
		[SerializeField] private AnimationCurve hitPauseCurve;
		[SerializeField] private float blockedStunTime = 1.25f;
		[SerializeField] private float parriedStunTime = 1.5f;
		[SerializeField, Tooltip("Fixed hit-pause (s) for the agent who deflected. Shorter than the attacker's, so recovering first is the reward.")]
		private float deflectorHitPause = 0.5f;
		[SerializeField, Tooltip("Fixed hit-pause (s) for the attacker whose blow was deflected. Ignores impact.")]
		private float deflectedHitPause = 1f;
		[SerializeField, Range(0f, 1f), Tooltip("Share of the stagger a deflect negated that the deflector still eats. The hitter takes the remainder.")]
		private float deflectEnduranceShare = 0.5f;

		[Header("Static / Charge Economy")]
		[SerializeField, Tooltip("Base Static (NE) restored per unit of threat (attack Mass × Power). The per-outcome fractions below scale it. Tune until a parry visibly refuels a charged counter.")]
		private float staticGain = 1f;
		[SerializeField, Range(0f, 1f), Tooltip("Fraction of Static built when deflecting or parrying an attack — both fully neutralise it.")]
		private float deflectStaticPercent = 1f;
		[SerializeField, Range(0f, 1f), Tooltip("Fraction of Static built when blocking/guarding an attack (partial guard scales this further by guard weight).")]
		private float blockStaticPercent = 0.5f;
		[SerializeField, Range(0f, 1f), Tooltip("Fraction of Static the attacker builds when landing a clean hit (Mass × Power basis).")]
		private float hitStaticPercent = 0.25f;
		[SerializeField, Range(0f, 1f), Tooltip("Fraction of Static the attacker builds on a crit — scaled by the attack's PIERCE (not Power), so precision self-sustains charge for Light builds.")]
		private float critStaticPercent = 1f;
		[SerializeField, Tooltip("Charge multiplier gained per unit of Static drained while charging. 0.005 = 100 Static drained → +0.5× power. The global Static→charge conversion.")]
		private float chargeConversionRatio = 0.005f;
		[SerializeField, Tooltip("Hard cap on the charge multiplier (e.g. 3 = up to 3× power / storm distance). Universal across charged moves.")]
		private float maxChargeMultiplier = 3f;

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

		// STORM: the charged upgrade to a stick. Extends the same leap and homes instead of committing to a
		// heading; both terms scale with charge, reaching full only at MaxChargeMultiplier.
		[Header("Storming")]
		[SerializeField, Min(0f), Tooltip("Extra distance (metres) a FULLY charged storm adds on top of StickRange. Scaled by charge (0 at no overcharge) and by the same StickRangeThrustScale lane as the stick, so a sweep storms less far than a thrust.")]
		private float stormRange = 6f;
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

		/// <summary>
		/// Universal wield speed factor for a strength/limb-mass <paramref name="wieldRatio"/>: the multiplier a melee
		/// swing runs at given how well the agent's strength wields the limb+weapon mass — &lt;1 when under-strength
		/// (heavy → slow, down to <c>strengthSpeedModRange.x</c>), &gt;1 when over-strength (up to <c>.y</c>). A
		/// mass-equal wield (ratio 1) or a natural strike (ratio 1) → 1. Single source of truth shared by the performer,
		/// move-selection and the AI's strike-timing so they all model the same swing speed.
		/// </summary>
		public float WieldSpeedFactor(float wieldRatio)
		{
			if (wieldRatio <= 1f)
			{
				return Mathf.Lerp(strengthSpeedModRange.x, 1f, Mathf.Pow(Mathf.Clamp01(wieldRatio), speedCurveExponent));
			}
			float extra = Mathf.Clamp01((wieldRatio - 1f) / (overStrengthFullRatio - 1f));
			return Mathf.Lerp(1f, strengthSpeedModRange.y, extra);
		}

		// EXERTION: what a swing costs in Energy is BODILY EFFORT, never output — a weapon that pierces well is no
		// more tiring than one that doesn't. Limb mass is the whole basis (weapon mass + 1% body, so Integrity and
		// carried load both raise it), which keeps every damage type paying while only Tenacity funds the pool.
		[Header("Exertion")]
		[SerializeField, Min(0f), Tooltip("Energy drained by a move authored at PerformCost 1 when swinging a limb of exactly ExertionRefMass. Default 100 ≈ a full level-1 Energy pool, so a 0.333 move at 15kg empties one.")]
		private float exertionCostAtRef = 100f;
		[SerializeField, Min(0.01f), Tooltip("Limb+weapon mass (kg) at which an ARMED move costs exactly its authored PerformCost × ExertionCostAtRef. The anchor the weapon lane pivots on.")]
		private float exertionRefMass = 2f;
		[SerializeField, Min(0.01f), Tooltip("StrikeMass (kg) at which an UNARMED move costs exactly its authored PerformCost × ExertionCostAtRef. Far higher than the weapon reference because body mass isn't held at arm's length — a kick throws the hip, not a lever.")]
		private float exertionRefBodyMass = 30f;
		[SerializeField, Range(0.1f, 2f), Tooltip("How hard mass bites, both lanes. 1 = proportional, above = accelerating. Also sets how fast cost keeps up with the pool as gear ranks up, since rank adds mass. At 1.2 a hammer costs ~5x a light blade.")]
		private float exertionMassExponent = 1.2f;
		[SerializeField, Min(0.01f), Tooltip("Lightest mass any strike is priced at. Limbless strikes (kicks, body rams) carry no limb mass at all and are floored here, so they still cost something.")]
		private float exertionFloorMass = 1f;

		/// <summary>
		/// Universal exertion factor: what a swing costs relative to its authored cost, as
		/// <c>(mass / referenceMass) ^ exponent</c> — 1 at the reference. Armed strikes pass limb+weapon mass against
		/// <see cref="ExertionRefMass"/>, unarmed ones pass StrikeMass against <see cref="ExertionRefBodyMass"/>.
		/// Single source of truth for the performer, move-selection and the AI's affordability gate.
		/// </summary>
		public float ExertionFactor(float mass, float referenceMass)
		{
			return Mathf.Pow(Mathf.Max(mass, exertionFloorMass) / Mathf.Max(referenceMass, 0.01f), exertionMassExponent);
		}
	}
}
