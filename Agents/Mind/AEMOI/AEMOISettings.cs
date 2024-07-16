using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "AEMOI Settings", menuName = "ScriptableObjects/AEMOI Settings")]
	public class AEMOISettings : ScriptableObject
	{
		public float EmotionSimulation = 1f;
		public float EmotionDamping = 0.1f;
		public StatOcton Personality;
	}
}
