using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "CombatSensesSettings", menuName = "ScriptableObjects/CombatSensesSettings")]
	public class CombatSensesSettings : ScriptableObject, IService
	{
		[Tooltip("How long after being out of view before an enemy is forgotten.")]
		public float ForgetTime = 10f;
	}
}
