using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// ScriptableObject defining a per-agent (or per-body-type) animation configuration.
	/// Holds blend trees for movement, landing animation config, and timing values.
	/// Can be injected into <see cref="PoserNode"/> to override its default serialized values.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(AgentMoveset), menuName = "Animation/" + nameof(AgentMoveset))]
	public class AgentMoveset : ScriptableObject
	{
		[Header("Movement Blend Trees")]
		[SerializeField] private PoseBlendMap groundedBlendTree;
		[SerializeField] private PoseBlendMap targetingBlendTree;
		[SerializeField] private PoseBlendMap flyingBlendTree;
		[SerializeField] private PoseBlendMap slidingBlendTree;

		[Header("Timing")]
		[SerializeField] private float positionBlendSpeed = 10f;
		[SerializeField] private float poseTransitionSpeed = 10f;
		[SerializeField] private float targetSwitchDuration = 1f;

		[Header("Landing")]
		[SerializeField, Tooltip("Single landing pose. Weight is determined by impact severity. Leave empty to skip.")]
		private PoseSequence landingPose;

		[SerializeField, Tooltip("Minimum impact speed (m/s) to trigger landing animation.")]
		private float landingMinImpact = 5f;

		[SerializeField, Tooltip("Impact speed (m/s) at which landing plays at full weight.")]
		private float landingMaxImpact = 20f;

		[SerializeField, Tooltip("Hold duration at full severity. Scaled down by severity at lower impacts.")]
		private float landingHoldDuration = 0.5f;

		[SerializeField] private float landingFadeOut = 0.3f;

		[SerializeField, Range(0f, 1f), Tooltip("How much movement control is reduced at full landing severity.")]
		private float landingControlReduction = 1f;

		public PoseBlendMap GroundedBlendTree => groundedBlendTree;
		public PoseBlendMap TargetingBlendTree => targetingBlendTree;
		public PoseBlendMap FlyingBlendTree => flyingBlendTree;
		public PoseBlendMap SlidingBlendTree => slidingBlendTree;

		public float PositionBlendSpeed => positionBlendSpeed;
		public float PoseTransitionSpeed => poseTransitionSpeed;
		public float TargetSwitchDuration => targetSwitchDuration;

		public PoseSequence LandingPose => landingPose;
		public float LandingMinImpact => landingMinImpact;
		public float LandingMaxImpact => landingMaxImpact;
		public float LandingHoldDuration => landingHoldDuration;
		public float LandingFadeOut => landingFadeOut;
		public float LandingControlReduction => landingControlReduction;
	}
}
