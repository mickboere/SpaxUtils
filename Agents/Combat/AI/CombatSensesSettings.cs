using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "CombatSensesSettings", menuName = "ScriptableObjects/CombatSensesSettings")]
	public class CombatSensesSettings : ScriptableObject, IService
	{
		[Header("Enemy Sense")]
		[Tooltip("How long after being out of view before an enemy is forgotten.")]
		public float ForgetTime = 10f;

		[Tooltip("Inverse-linear distance falloff applied to all enemy stimuli.\n" +
			"1/(1+k*d): k=0.1 → 50% strength at 10m, k=0.2 → 50% at 5m.\n" +
			"0 = no falloff.")]
		public float DistanceFalloffK = 0.1f;

		[Header("Ally Sense")]
		[Tooltip("Seconds without sight before a tracked ally is forgotten.")]
		public float AllyForgetTime = 30f;

		[Tooltip("Distance at which E (Follow) stim reaches 1. Below this the stim scales linearly to 0.")]
		public float FollowRange = 10f;

		[Tooltip("SW: ally health ratio below this triggers Supply stim.")]
		public float SupplyHealthThreshold = 0.5f;

		[Tooltip("W: ally health ratio below this (and in danger) triggers Shield stim.")]
		public float ShieldHealthThreshold = 0.3f;

		[Tooltip("S: own Emotion.S above this triggers Retreat-to-ally stim.")]
		public float FearToRetreatThreshold = 3f;

		[Tooltip("SE: own emotion AbsSum below this triggers Rally stim.")]
		public float RallyMaxMotivation = 2f;

		[Header("Aggro Accumulation")]
		[Tooltip("How much negative relation is added per unit of impact when this agent is hit.")]
		public float AggroRelationGain = 0.3f;

		[Tooltip("Maximum negative relation that can be accumulated against a single attacker's ID.")]
		public float MaxAggroRelation = 2.0f;
	}
}
