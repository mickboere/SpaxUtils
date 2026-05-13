using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "CombatSensesSettings", menuName = "ScriptableObjects/CombatSensesSettings")]
	public class CombatSensesSettings : ScriptableObject, IService
	{
		[Header("Enemy Sense")]
		[Tooltip("How long after being out of view before an enemy is forgotten.")]
		public float ForgetTime = 10f;

		[Tooltip("Seconds of closing-time ahead at which approachDanger begins building. " +
			"The InOutSine easing means it rises slowly at distance and accelerates as the enemy closes.")]
		public float ApproachHorizonSeconds = 1.5f;

		[Header("Distance & Readiness")]
		[Tooltip("Exponential decay rate applied to all enemy stimuli beyond the enemy's attack reach.\n" +
			"exp(-K * max(0, distance - activeReach)). K=0.3: 55% at 2m beyond, 5% at 10m beyond.")]
		public float ExponentialFalloffK = 0.3f;

		[Tooltip("Octology cross-state cascade scale: an enemy's emotional state at wheel position X " +
			"additively contributes to our drive at position X+1 (clockwise). " +
			"0.5 = a fully-saturated enemy channel adds up to half MAX_STIM.")]
		[Range(0f, 2f)]
		public float CrossStateScale = 0.1f;

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

		[Tooltip("Satisfaction applied to all drives per extra targeter per second, scaled by SE inclination.\n" +
			"Cooperative agents (high SE inclination) naturally cede shared targets; ruthless agents (low SE) ignore it.")]
		[Range(0f, 2f)]
		public float SharedTargetRelaxRate = 0.5f;

		[Header("Aggro Accumulation")]
		[Tooltip("How much negative relation is added per unit of impact when this agent is hit.")]
		public float AggroRelationGain = 0.3f;

		[Tooltip("Maximum negative relation that can be accumulated against a single attacker's ID.")]
		public float MaxAggroRelation = 2.0f;
	}
}
