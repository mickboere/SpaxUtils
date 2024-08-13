using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Configurable <see cref="IRangedCombatMove"/> asset that contains data required for performing a ranged combat move.
	/// </summary>
	[CreateAssetMenu(fileName = "RangedCombatMove", menuName = "ScriptableObjects/Combat/RangedCombatMove")]
	public class RangedCombatMove : BaseCombatMove, IRangedCombatMove
	{
		public GameObject ProjectilePrefab => projectilePrefab;
		public string InstanceLocation => instanceLocation;
		public float InstanceDelay => instanceDelay;

		[Header("Ranged")]
		[SerializeField] private GameObject projectilePrefab;
		[SerializeField, ConstDropdown(typeof(ITransformLookupIdentifiers), includeEmpty: true, showAdress: true)] private string instanceLocation;
		[SerializeField] private float instanceDelay;
	}
}
