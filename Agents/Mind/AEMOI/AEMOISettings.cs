using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "AEMOISettings", menuName = "AEMOI/AEMOI Settings")]
	public class AEMOISettings : ScriptableObject, IService
	{
		[Header("Stats")]
		[Tooltip("Asset defining emotional inclination stats.")]
		public StatOctadAsset Inclination;
		[Tooltip("Asset defining the 8 personality stats.")]
		public StatOctadAsset Personality;

		[Header("Emotion")]
		[Tooltip("The amount of damping applied to stimulation values.")]
		public float StimDamping = 5f;
		[Tooltip("Factor by which emotion the emotions interpolate back to 0.")]
		public float EmotionDecay = 0.05f;
		[Tooltip("Above this value, emotion is considered overflow and will be redistributed. 0 disables overflow handling.")]
		[Range(0f, AEMOI.MAX_STIM)]
		public float OverflowThreshold = 1f;
		[Tooltip("Fraction of overflow moved per second into other directions. Higher = faster spread, lower = more lingering peaks.")]
		public float OverflowRedistributionRate = 1f;
		[Tooltip("How strongly overflow prefers neighbouring axes over distant/opposite ones." +
			"\n0 = ignore distance;\n0.25 = opposing * 0.5;\n1 = opposing * 0.2;\n4 = opposing * 0.06")]
		public float OverflowDistanceBias = 1f;

		[Header("Stimulated Satisfaction")]
		[Tooltip("How strongly the balance between a direction and its opposite affects satisfaction. 0 = disabled, 1 = full strength.")]
		public float AxisBalanceSatisfactionStrength = 0.75f;
		[MinMaxRange(0f, 2f), Tooltip("Multiplier range to satisfaction from axis balance." +
			"\nLower minimum values make dominant directions drain very slowly." +
			"\nHigher maximum values let opposed directions drain very quickly.")]
		public Vector2 AxisBalanceSatisfactionRange = new Vector2(0.25f, 2f);

		[Header("Emotion Aggregation")]
		[Tooltip("Smooth rate for the Emotion aggregate (lerp factor per second). ~0.25 yields a 2-4s half-life.")]
		public float EmotionSmoothRate = 0.25f;

		[Header("Balance")]
		[Tooltip("Inertia dampener on the directed (per-target) component of Balance. Higher = slower Balance response to target stimuli.")]
		public float BalanceInertiaK = 1.0f;
		[Range(0f, 1f), Tooltip("Weight of Inclination in the Balance computation.")]
		public float BalanceInclinationWeight = 1f;
		[Range(0f, 1f), Tooltip("Weight of Personality in the Balance computation.")]
		public float BalancePersonalityWeight = 1f;
		[Range(0f, 1f), Tooltip("Weight of Emotion in the Balance computation. 0 = no emotional colouring; tune up to taste.")]
		public float BalanceEmotionWeight = 0f;

		[Header("Behaviour Switching")]
		[Tooltip("Extra relative strength required for a new behaviour with the same priority to override the current one.\n" +
		 "0 = no inertia, 0.25 = needs 25% more strength.")]
		[Range(0f, 1f)]
		public float BehaviourSwitchThreshold = 0.25f;

		[Header("Combat Spacing")]
		[Tooltip("Max extra standoff distance (world units) a fully cautious agent (Balance.S = 1) keeps from a foe; scales from 0 at neutral S (0.5). The shared 'back away when vulnerable' spacing tell used by every standoff/strafe behaviour.")]
		public float CautiousSpacingMax = 3f;

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
