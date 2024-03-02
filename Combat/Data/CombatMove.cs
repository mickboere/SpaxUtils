using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Configurable <see cref="ICombatMove"/> asset that can be evaluated to return combat poses.
	/// </summary>
	[CreateAssetMenu(fileName = "CombatMove", menuName = "ScriptableObjects/CombatMove")]
	public class CombatMove : PerformanceMove, ICombatMove
	{
		#region Properties
		public List<string> HitBoxes => hitBoxes;
		public float HitDetectionDelay => hitDetectionDelay;
		public Vector3 Inertia => momentum;
		public float ForceDelay => forceDelay;
		public string Limb => limb;
		public float Strength => strength;
		public float Offence => offence;
		public float Piercing => piercing;

		#endregion // Properties

		[Header("Hit detection")]
		[SerializeField, ConstDropdown(typeof(ITransformLookupIdentifiers), showAdress: true)] private List<string> hitBoxes;
		[SerializeField] private float hitDetectionDelay = 0f;

		[Header("Momentum")]
		[SerializeField] private Vector3 momentum;
		[SerializeField] private float forceDelay;

		[Header("Stats")]
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants), filter: AgentStatIdentifiers.SUB_STAT)] private string limb;
		[SerializeField, Range(0f, 1f), Tooltip("Percentage of user's strength transfered into hit.")] private float strength = 1f;
		[SerializeField, Range(0f, 1f), Tooltip("Percentage of user's offence transfered into hit.")] private float offence = 1f;
		[SerializeField, Range(0f, 1f), Tooltip("Percentage of user's piercing transfered into hit.")] private float piercing = 1f;
	}
}
