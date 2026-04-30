using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "CombatSensesSettings", menuName = "ScriptableObjects/CombatSensesSettings")]
	public class CombatSensesSettings : ScriptableObject, IService
	{
		[Tooltip("How long after being out of view before an enemy is forgotten.")]
		public float ForgetTime = 10f;

		[Tooltip("Inverse-linear distance falloff applied to all enemy stimuli.\n" +
			"1/(1+k*d): k=0.1 → 50% strength at 10m, k=0.2 → 50% at 5m.\n" +
			"0 = no falloff.")]
		public float DistanceFalloffK = 0.1f;
	}
}
