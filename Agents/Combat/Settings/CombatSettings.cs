using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "CombatSettings", menuName = "ScriptableObjects/Combat/CombatSettings")]
	public class CombatSettings : ScriptableObject, IService
	{
		public Vector2 HitPauseRange => hitPauseRange;
		public AnimationCurve HitPauseCurve => hitPauseCurve;

		[SerializeField, MinMaxRange(0f, 1f)] private Vector2 hitPauseRange = new Vector2(0.1f, 0.75f);
		[SerializeField] private AnimationCurve hitPauseCurve;
	}
}
