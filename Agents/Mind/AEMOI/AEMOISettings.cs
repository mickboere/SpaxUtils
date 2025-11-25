using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "AEMOISettings", menuName = "AEMOI/AEMOI Settings")]
	public class AEMOISettings : ScriptableObject, IService
	{
		[Tooltip("The amount of damping applied to stimulation values.")]
		public Vector8 StimDamping = Vector8.One * 10f;
		public float EmotionDispersion = 0.0025f;
		public float EmotionDecay = 0.05f;
		public StatOctad Inclination;
		public StatOctad Personality;
	}
}
