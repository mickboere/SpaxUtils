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
		public float DeflectedStunTime => deflectedStunTime;
		public float StaticGain => staticGain;
		public float ChargeConversionRatio => chargeConversionRatio;
		public float MaxChargeMultiplier => maxChargeMultiplier;
		public float ChargeBalance => chargeBalance;
		public float PerformBalance => performBalance;
		public float Restitution => restitution;
		public float MeleeFloorPadding => meleeFloorPadding;
		public float MeleeFloorStiffness => meleeFloorStiffness;
		public AnimationCurve RearExposureCurve => rearExposureCurve;

		[Header("Hit Pause Settings")]
		[SerializeField, MinMaxRange(0f, 1f)] private Vector2 hitPauseReceiver = new Vector2(0.05f, 0.75f);
		[SerializeField, MinMaxRange(0f, 1f)] private Vector2 hitPauseSender = new Vector2(0.05f, 0.25f);
		[SerializeField] private AnimationCurve hitPauseCurve;
		[SerializeField] private float blockedStunTime = 1.25f;
		[SerializeField] private float parriedStunTime = 1.5f;
		[SerializeField] private float deflectedStunTime = 1f;

		[Header("Static / Charge Economy")]
		[SerializeField, Tooltip("Base Static (NE) restored per unit of threat (attack Mass × Power). Parry/deflect = full, block = 50%, landing a hit = 25%. Tune until a parry visibly refuels a charged counter.")]
		private float staticGain = 1f;
		[SerializeField, Tooltip("Charge multiplier gained per unit of Static drained while charging. 0.005 = 100 Static drained → +0.5× power. The global Static→charge conversion.")]
		private float chargeConversionRatio = 0.005f;
		[SerializeField, Tooltip("Hard cap on the charge multiplier (e.g. 3 = up to 3× power / storm distance). Universal across charged moves.")]
		private float maxChargeMultiplier = 3f;

		[Header("Balance")]
		[SerializeField, Range(0.01f, 1f), Tooltip("How much balance is maintained while charging a melee move. Universal across melee moves.")]
		private float chargeBalance = 1f;
		[SerializeField, Range(0.01f, 1f), Tooltip("How much balance is maintained while performing a melee move. Universal across melee moves.")]
		private float performBalance = 1f;

		[Header("Physics")]
		[SerializeField, Range(0f, 1f), Tooltip("Elasticity of the inertia-sharing clash when a hit lands. 0 = perfectly inelastic (both bodies hold the gap), 1 = fully elastic (they bounce apart). Scales both the receiver's knockback and the hitter's self-brake by (1 + restitution).")]
		private float restitution = 0f;
		[SerializeField, Range(0f, 3f), Tooltip("Melee separation floor: absolute width (metres) of the no-go ring added OUTSIDE the combined top-down radii. The lunge is sprung back out within this ring; the combined radii itself is an impenetrable wall. Absolute (not a fraction) so it stays a fixed buffer even against giant enemies. Keep below (reach − combined radii) or attacks can't connect.")]
		private float meleeFloorPadding = 0.5f;
		[SerializeField, Min(0f), Tooltip("Spring stiffness of the melee separation floor (auto critically-damped). Higher = the lunge is stopped sooner/harder before it reaches the inner wall.")]
		private float meleeFloorStiffness = 150f;

		[Header("Vulnerability")]
		[SerializeField, Tooltip("Maps how exposed the receiver is to a hit based on the angle it lands from, into a 0..1 rear-exposure factor that lerps the receiver's Vulnerability toward 1 (full crit). INPUT (X, 0..1): the hit's angle relative to the receiver's facing - 0 = struck dead-on from the front, 0.5 = struck from the side, 1 = struck from directly behind. OUTPUT (Y, 0..1): exposure - 0 = no added vulnerability (use the receiver's base Vulnerability stat), 1 = fully exposed (Vulnerability forced to 1, guaranteeing a crit if the hit couples). Default shape: front/sides approx 0, ramping up to 1 at the rear.")]
		private AnimationCurve rearExposureCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 0f), new Keyframe(1f, 1f));
	}
}
