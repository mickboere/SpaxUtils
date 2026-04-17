using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	/// <summary>
	/// Configurable <see cref="IMeleeCombatMove"/> asset that contains data required for performing a melee combat move.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(MeleeCombatMove), menuName = "Performance/Combat/" + nameof(MeleeCombatMove))]
	public class MeleeCombatMove : BaseCombatMove, IMeleeCombatMove
	{
		public MeleeAttackDirection AttackDirection => attackDirection;
		public List<string> HitBoxes => hitBoxes;
		public float HitDetectionDelay => hitDetectionDelay;
		public bool CustomDirection => customDirection;
		public Vector3 HitDirection => hitDirection;
		public Vector3 Inertia => inertia;
		public float InertiaDelay => inertiaDelay;
		public float StormDistance => stormDistance;
		public bool PrelongCharge => prelongCharge;
		public float ProlongThreshold => prolongThreshold;
		public string Limb => limb;
		public float Piercing => piercing;
		public float Power => power;
		public float Precision => precision;
		public float ChargeBalance => chargeBalance;
		public float PerformBalance => performBalance;

		[Header("Hit detection")]
		[SerializeField, FormerlySerializedAs("attackType")] private MeleeAttackDirection attackDirection;
		[SerializeField, ConstDropdown(typeof(ITransformLookupIdentifiers), showAdress: true)] private List<string> hitBoxes;
		[SerializeField] private float hitDetectionDelay = 0f;
		[SerializeField, HideInInspector] private bool customDirection;
		[SerializeField, Conditional(nameof(customDirection), false, true, false)] private Vector3 hitDirection;

		[Header("Momentum")]
		[SerializeField, FormerlySerializedAs("momentum")] private Vector3 inertia;
		[SerializeField, FormerlySerializedAs("forceDelay")] private float inertiaDelay;
		[SerializeField, Tooltip("Whether the charge pose should be held until within attack release range.")] private bool prelongCharge;
		[SerializeField] private float stormDistance = 5f;
		[SerializeField, Tooltip("Velocity above which the performance should be prolonged.")] private float prolongThreshold = 1f;

		[Header("Stats")]
		[SerializeField, ConstDropdown(typeof(IStatIdentifiers), filter: AgentStatIdentifiers.SUB_STAT)] private string limb;
		[SerializeField, Range(0f, 2f), Tooltip("Percentage of user's Piercing transfered into hit."), FormerlySerializedAs("offence")] private float piercing = 1f;
		[SerializeField, Range(0f, 2f), Tooltip("Percentage of user's Power transfered into hit.")] private float power = 1f;
		[SerializeField, Range(0f, 2f), Tooltip("Percentage of user's Precision transfered into hit.")] private float precision = 1f;
		[SerializeField, Range(0.01f, 1f), Tooltip("How much balance is maintained while charging.")] private float chargeBalance = 1f;
		[SerializeField, Range(0.01f, 1f), Tooltip("How much balance is maintained while performing.")] private float performBalance = 1f;
	}
}
