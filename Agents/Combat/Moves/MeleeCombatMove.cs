using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	/// <summary>
	/// Configurable <see cref="IMeleeCombatMove"/> asset that contains data required for performing a melee combat move.
	/// </summary>
	[CreateAssetMenu(fileName = "MeleeCombatMove", menuName = "ScriptableObjects/Combat/MeleeCombatMove")]
	public class MeleeCombatMove : BaseCombatMove, IMeleeCombatMove
	{
		public MeleeAttackType AttackType => attackType;
		public List<string> HitBoxes => hitBoxes;
		public float HitDetectionDelay => hitDetectionDelay;
		public Vector3 Inertia => inertia;
		public float InertiaDelay => inertiaDelay;
		public float ProlongThreshold => prolongThreshold;
		public string Limb => limb;
		public float Power => power;
		public float Offence => offence;
		public float ChargeBalance => chargeBalance;
		public float PerformBalance => performBalance;

		[Header("Hit detection")]
		[SerializeField] private MeleeAttackType attackType;
		[SerializeField, ConstDropdown(typeof(ITransformLookupIdentifiers), showAdress: true)] private List<string> hitBoxes;
		[SerializeField] private float hitDetectionDelay = 0f;

		[Header("Momentum")]
		[SerializeField, FormerlySerializedAs("momentum")] private Vector3 inertia;
		[SerializeField, FormerlySerializedAs("forceDelay")] private float inertiaDelay;
		[SerializeField, Tooltip("Velocity above which the performance should be prolonged.")] private float prolongThreshold = 1f;

		[Header("Stats")]
		[SerializeField, ConstDropdown(typeof(IStatIdentifiers), filter: AgentStatIdentifiers.SUB_STAT)] private string limb;
		[SerializeField, Range(0f, 2f), Tooltip("Percentage of user's strength transfered into hit.")] private float power = 1f;
		[SerializeField, Range(0f, 2f), Tooltip("Percentage of user's offence transfered into hit.")] private float offence = 1f;
		[SerializeField, Range(0.01f, 1f), Tooltip("How much balance is maintained while charging.")] private float chargeBalance = 1f;
		[SerializeField, Range(0.01f, 1f), Tooltip("How much balance is maintained while performing.")] private float performBalance = 1f;
	}
}
