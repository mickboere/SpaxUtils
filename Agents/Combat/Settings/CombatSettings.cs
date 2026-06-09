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
	}
}
