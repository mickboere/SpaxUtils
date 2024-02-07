using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "CombatSettings", menuName = "ScriptableObjects/Combat/CombatSettings")]
	public class CombatSettings : ScriptableObject, IService
	{
		public float MaxHitPause => maxHitPause;
		public AnimationCurve HitPauseCurve => hitPauseCurve;

		[SerializeField] private float maxHitPause = 0.5f;
		[SerializeField] private AnimationCurve hitPauseCurve;
	}
}
