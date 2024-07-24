using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "AEMOISettings", menuName = "AEMOI/AEMOI Settings")]
	public class AEMOISettings : ScriptableObject
	{
		public float EmotionSimulation = 1f;
		public float EmotionDamping = 0.1f;
		public StatOcton Personality;
	}
}
