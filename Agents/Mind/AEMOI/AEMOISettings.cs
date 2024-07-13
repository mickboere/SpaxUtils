using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "AEMOI Settings", menuName = "ScriptableObjects/AEMOI Settings")]
	public class AEMOISettings : ScriptableObject
	{
		public float emotionSimulation = 1f;
		public float emotionDamping = 0.1f;
	}
}
