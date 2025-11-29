using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "AEMOISettings", menuName = "AEMOI/AEMOI Settings")]
	public class AEMOISettings : ScriptableObject, IService
	{
		[Header("Stats")]
		[Tooltip("Octad defining emotional inclination stats.")]
		public StatOctad Inclination;
		[Tooltip("Octad defining the 8 personality stats.")]
		public StatOctad Personality;

		[Header("Emotion")]
		[Tooltip("The amount of damping applied to stimulation values.")]
		public Vector8 StimDamping = Vector8.One * 10f;
		[Tooltip("Factor by which emotion the emotions interpolate back to 0.")]
		public float EmotionDecay = 0.05f;
		[Tooltip("Above this value, emotion is considered overflow and will be redistributed. 0 disables overflow handling.")]
		[Range(0f, AEMOI.MAX_STIM)]
		public float OverflowThreshold = 1f;
		[Tooltip("Fraction of overflow moved per second into other directions. Higher = faster spread, lower = more lingering peaks.")]
		public float OverflowRedistributionRate = 1f;
	}
}
