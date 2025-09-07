using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "CombatSettings", menuName = "ScriptableObjects/Combat/CombatSettings")]
	public class CombatSettings : ScriptableObject, IService
	{
		public Vector2 HitPauseReceiver => hitPauseReceiver;
		public Vector2 HitPauseSender => hitPauseSender;
		public AnimationCurve HitPauseCurve => hitPauseCurve;
		public float BlockedStunTime => blockedStunTime;
		public float ParriedStunTime => parriedStunTime;
		public float DeflectedStunTime => deflectedStunTime;

		[SerializeField, MinMaxRange(0f, 1f)] private Vector2 hitPauseReceiver = new Vector2(0.05f, 0.75f);
		[SerializeField, MinMaxRange(0f, 1f)] private Vector2 hitPauseSender = new Vector2(0.05f, 0.25f);
		[SerializeField] private AnimationCurve hitPauseCurve;
		[SerializeField] private float blockedStunTime = 1.25f;
		[SerializeField] private float parriedStunTime = 1.5f;
		[SerializeField] private float deflectedStunTime = 1f;
	}
}
