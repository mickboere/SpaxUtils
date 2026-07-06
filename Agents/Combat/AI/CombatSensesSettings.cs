using UnityEngine;
using UnityEngine.Serialization;

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

		[Header("Combat Spacing")]
		[FormerlySerializedAs("CautiousSpacingMax")]
		[Tooltip("World-unit scale of the shared trait-driven StandoffOffset (AEMOIBehaviourAsset). At ±1 the standoff shifts this far in (aggressive) or out (cautious/winded/outmatched/merciful). Used by every standoff/strafe behaviour.")]
		public float StandoffMax = 5f;

		[Tooltip("How fast the held spacing distance chases its target, as [dull, sharp] interpolated over Drive.NE. HIGHER = snappier (hugs the ideal spacing); LOWER = laggier (wobbles off it). x = rate at Drive.NE 0 (dull), y = at Drive.NE 1 (sharp).")]
		[MinMaxRange(0f, 20f)]
		public Vector2 TrackingRate = new Vector2(2f, 12f);

		[Tooltip("Repositioning-input scale at MIN mobility (Drive.E = 0): low = a dull agent barely strafes/backs off and gets caught. Floored above 0 so it still creeps, not freezes. Reaches full input at curve = 1.")]
		[Range(0f, 1f)]
		public float MovementScaleFloor = 0.25f;

		[Tooltip("Shapes Drive.E -> movement scale (x = Drive.E, y feeds the floor lerp). Rise fast and PLATEAU at 1 so mobile agents move at full power and only the low end is damped. NOTE: x is Drive.E, which is difficulty-COMPRESSED — read the Debuddy 'drv E' value to place the plateau; it sits well below raw difficulty.")]
		public AnimationCurve MovementScaleCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.66f, 1f), new Keyframe(1f, 1f));
	}
}
