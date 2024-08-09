using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "AEMOISettings", menuName = "AEMOI/AEMOI Settings")]
	public class AEMOISettings : ScriptableObject
	{
		public float EmotionDispersion = 0.05f;
		public float EmotionDamping = 0.1f;
		public StatOcton Personality;
	}
}
