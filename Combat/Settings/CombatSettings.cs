using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "CombatSettings", menuName = "ScriptableObjects/Combat/CombatSettings")]
	public class CombatSettings : ScriptableObject, IService
	{
		public float MinAbsorbtion => minAbsorbtion;
		public float MaxAbsorbtion => maxAbsorbtion;
		public float MaxHitPause => maxHitPause;
		public AnimationCurve HitPauseCurve => hitPauseCurve;

		[SerializeField, Range(0f, 1f)] private float minAbsorbtion = 0.5f;
		[SerializeField, Range(0f, 1f)] private float maxAbsorbtion = 1f;
		[SerializeField] private float maxHitPause = 0.5f;
		[SerializeField] private AnimationCurve hitPauseCurve;
	}
}
