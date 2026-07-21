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
		[MinMaxRange(0f, 12f), Tooltip("Stimulation tracker speed as a [min,max] range, interpolated per axis by RateCurve.Evaluate(inclination). Low inclination (= low difficulty, via the trait remap) → min (sluggish); full inclination → max (snappy). x=min, y=max.")]
		public Vector2 StimulationRate = new Vector2(1f, 6f);
		[Range(0f, 1f), Tooltip("Per-axis rise/fall asymmetry keyed on each pole's SHARE of its axis pair (inc[i] / (inc[i] + inc[opposite])). The dominant pole rises fast + falls slow (build & linger = grudge); the recessive pole rises slow and is pushed back out fast by its opposite — so a drive's drop-rate IS the opposing element's strength (high aggression = fast anger rise AND fast fear drop). Being a ratio, pair magnitude (and thus difficulty) stays out of it; that lives in StimulationRate. Higher = stronger split; 0 = no asymmetry (every axis rises AND falls at full rate); 1 = the recessive pole can't rise and the dominant can't fall.")]
		public float StimulationInclinationBias = 0.75f;
		[MinMaxRange(0f, 2f), Tooltip("Emotion envelope speed as a [min,max] range, interpolated per axis by RateCurve.Evaluate(inclination) — same difficulty mechanism as StimulationRate. x=min, y=max.")]
		public Vector2 EmotionRate = new Vector2(0.1f, 0.6f);
		[Tooltip("Maps inclination [0,1] → [0,1] before it interpolates the Stimulation/Emotion rate ranges. Inclination bottoms at ~0.19 (difficulty floor 0.1 × remap floor 0.1), so make this CONVEX (flat-low, steep near 1) to keep everything below high difficulty genuinely slow — only near-max inclination reaches the max rate.")]
		public AnimationCurve RateCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
		[Range(0f, 1f), Tooltip("Pair-share MIRROR on the emotion rate (same quantity as StimulationInclinationBias): the dominant pole of an axis pair rises fast and falls slow — it builds and lingers; the recessive pole rises slow and falls fast. So emotion only climbs on axes the agent actually leans toward (keeps Balance from flattening on a co-active opposite pole). 0 = no asymmetry (uniform); 1 = the recessive pole never rises / the dominant never falls.")]
		public float EmotionInclinationBias = 0.75f;
		[Range(0f, 1f), Tooltip("Global multiplier on the emotion envelope's FALL rate only — inclination-independent, unlike the bias above. 1 = symmetric with the rise. Below 1 = emotion is retained between engagements and accumulates over a long battle, so late-fight behaviour escalates. Does NOT touch the Stimulation tracker, which must keep tracking Demand faithfully (so a calm situation still drains Motivation; what persists here is the agent's CAPACITY to respond intensely). ~0.5 approximates the pre-pair-share feel; go lower for real accumulation.")]
		public float EmotionFallMultiplier = 0.5f;

		[Header("Overflow")]
		[Tooltip("Stimulation above this bleeds out to other axes instead of accumulating. Effectively the ceiling on a single drive — and since Emotion chases max|Stimulation|, the ceiling on Emotion too (raise toward MAX_STIM to let a drive climb higher). 0 disables overflow.")]
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
		[Range(1f, 4f), Tooltip("Contrast gain (center steepness) of the soft logistic sigmoid sharpening each Balance axis away from neutral (0.5). Asymptotes toward 0/1 instead of hard-clipping, so strong leans stay distinct and the extremes never quite reach 0/1. Symmetric — a pole and its opposite still sum to 1. Higher = steeper / more decisive.")]
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
