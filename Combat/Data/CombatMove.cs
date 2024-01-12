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
		public List<ActCombatPair> FollowUps => followUps;
		public Vector3 Inertia => momentum;
		public float ForceDelay => forceDelay;
		public string StrengthStat => strengthStat;
		public bool Offensive => offensive;
		public string OffenceStat => offenceStat;
		public float Offensiveness => offensiveness;
		public IList<StatCost> PerformCost => performCost;

		#endregion // Properties

		[Header("Combat")]
		[SerializeField, ConstDropdown(typeof(ITransformLookupIdentifiers), showAdress: true)] private List<string> hitBoxes;
		[SerializeField] private List<ActCombatPair> followUps;

		[Header("Forces")]
		[SerializeField] private Vector3 momentum;
		[SerializeField] private float forceDelay;

		[Header("Stats")]
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants)), Tooltip("Influences force applied to target.")] private string strengthStat = AgentStatIdentifiers.STRENGTH;
		[SerializeField, HideInInspector] private bool offensive;
		[SerializeField, Conditional(nameof(offensive), drawToggle: true), ConstDropdown(typeof(IStatIdentifierConstants))] private string offenceStat = AgentStatIdentifiers.OFFENCE;
		[SerializeField, Conditional(nameof(offensive)), Range(0f, 2f)] private float offensiveness = 1f;
		[SerializeField] private List<StatCost> performCost;
	}
}
