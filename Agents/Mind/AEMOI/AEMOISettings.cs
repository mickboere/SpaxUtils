using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "AEMOISettings", menuName = "AEMOI/AEMOI Settings")]
	public class AEMOISettings : ScriptableObject, IService
	{
		[Header("Stats")]
		[Tooltip("Asset defining emotional inclination stats.")]
		public StatOctadAsset Inclination;
		[Range(0f, 1f), Tooltip("Stat-derived Inclination is remapped [0,1] -> [floor,1] at init so no axis starts dead. Preserves distribution shape (remap, not clamp).")]
		public float InclinationFloor = 0.1f;
		[Tooltip("Asset defining the 8 personality stats.")]
		public StatOctadAsset Personality;
		[Range(0f, 1f), Tooltip("Stat-derived Personality is remapped [0,1] -> [floor,1] at init so no axis starts dead. Preserves distribution shape (remap, not clamp).")]
		public float PersonalityFloor = 0.1f;

		[Header("Tracker / Envelope")]
		[Tooltip("Always-actionable headroom motivation keeps above the Emotion envelope (cap = min(Emotion + BaseFloor, MAX_STIM)). 1 = base behaviours always available; raise to gate them harder.")]
		public float BaseFloor = 1f;
		[Tooltip("Base speed at which Stimulation tracks toward Demand (framerate-independent). Reaction speed for a fully-inclined axis at full difficulty; difficulty scales this via the remapped inclination that drives it (no separate multiplier). Higher = snappier.")]
		public float StimulationRate = 6f;
		[Range(0f, 1f), Tooltip("How hard inclination splits the Stimulation tracker's rise vs fall. Strong inclination → fast rise / slow fall (drive builds & lingers = grudge); weak → slow rise / fast fall (leaks, never accumulates). 0 = inclination has no effect; 1 = weak/reluctant direction frozen. THIS is the drive-selection differentiator.")]
		public float StimulationInclinationBias = 0.75f;
		[Tooltip("Base Emotion envelope speed toward |Stimulation|, before the inclination mirror. THE ramp knob: how fast the softcap lifts / rage builds on a fully-inclined axis (difficulty scales it via the remapped inclination).")]
		public float EmotionRate = 0.5f;
		[Range(0f, 1f), Tooltip("Inclination MIRROR on the emotion rate: a strong-inclination axis rises fast (EmotionRate) and falls slow (×(1-bias)) — it builds and lingers; a weak axis rises slow (×(1-bias)) and falls fast — it barely accumulates. So emotion only climbs on axes the agent cares about (keeps Balance from flattening on a co-active opposite pole). 0 = no inclination effect (uniform); 1 = weak axes never rise / strong axes never fall.")]
		public float EmotionInclinationBias = 0.75f;

		[Header("Overflow")]
		[Tooltip("Above this value, stimulation is considered overflow and will be redistributed. 0 disables overflow handling.")]
		[Range(0f, AEMOI.MAX_STIM)]
		public float OverflowThreshold = 1f;
		[Tooltip("Fraction of overflow moved per second into other directions. Higher = faster spread, lower = more lingering peaks.")]
		public float OverflowRedistributionRate = 1f;
		[Tooltip("How strongly overflow prefers neighbouring axes over distant/opposite ones." +
			"\n0 = ignore distance;\n0.25 = opposing * 0.5;\n1 = opposing * 0.2;\n4 = opposing * 0.06")]
		public float OverflowDistanceBias = 1f;

		[Header("Emotion Normalization")]
		[Range(0.1f, 0.9f), Tooltip("Normalized [0,1] weight a just-actionable emotion (raw stim = 1) maps to via EmotionNormalized's concave curve. " +
			"Higher = actionable emotions carry more weight and the high end (1..MAX_STIM) is compressed harder. 0.1 ≈ linear; 0.3 ≈ sqrt.")]
		public float EmotionNormalizationAnchor = 0.3f;

		[Header("Balance")]
		[Tooltip("Inertia dampener on the directed (per-target) component of Balance. Higher = slower Balance response to target stimuli.")]
		public float BalanceInertiaK = 1.0f;
		[Range(0f, 1f), Tooltip("Weight of Inclination in the Balance computation.")]
		public float BalanceInclinationWeight = 1f;
		[Range(0f, 1f), Tooltip("Weight of Personality in the Balance computation.")]
		public float BalancePersonalityWeight = 1f;
		[Range(0f, 1f), Tooltip("Weight of Emotion in the Balance computation. 0 = no emotional colouring; tune up to taste.")]
		public float BalanceEmotionWeight = 0.5f;
		[Range(1f, 4f), Tooltip("Lean: contrast gain that sharpens each Balance axis away from neutral (0.5). Applied symmetrically so a pole and its opposite still sum to 1. 1 = no change; 2 = a 0.6 lean becomes 0.7, a 0.55 becomes 0.6. Makes genuine leans read strong without touching the weights.")]
		public float BalanceLean = 2f;

		[Header("Behaviour Switching")]
		[Tooltip("Extra relative strength required for a new behaviour with the same priority to override the current one.\n" +
		 "0 = no inertia, 0.25 = needs 25% more strength.")]
		[Range(0f, 1f)]
		public float BehaviourSwitchThreshold = 0.25f;

		[Header("Visuals")]
		[Tooltip("Colors for the 8 emotion axes: N, NE, E, SE, S, SW, W, NW.")]
		public Color[] EmotionColors = new Color[8]
		{
			new(1f, 0.3f, 0.1f),   // N  — Fire (red-orange)
			new(1f, 0.95f, 0.1f),  // NE — Light (yellow)
			new(0.2f, 0.8f, 1f),   // E  — Air (sky blue)
			new(1f, 0.3f, 0.6f),   // SE — Spirit (pink)
			new(0.1f, 0.5f, 1f),   // S  — Water (blue)
			new(0.2f, 0.75f, 0.2f),// SW — Nature (green)
			new(1f, 0.5f, 0.05f),  // W  — Earth (orange)
			new(0.15f, 0.05f, 0.3f)// NW — Void (dark purple)
		};
	}
}
