﻿using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "CombatSensesSettings", menuName = "ScriptableObjects/CombatSensesSettings")]
	public class CombatSensesSettings : ScriptableObject
	{
		[Tooltip("Relative to view distance, within which range does an agent pose a threat."), Range(0f, 1f)] public float ThreatRange = 1f;
		public AnimationCurve ThreatCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[Tooltip("Relative to view distance, within which range does an agent incite anger."), Range(0f, 1f)] public float InciteRange = 0.5f;
		public AnimationCurve InciteCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		public Vector8 StimDamping = Vector8.One * 2f;
		[Tooltip("How long after being out of view before an enemy is forgotten.")] public float ForgetTime = 10f;
	}
}
