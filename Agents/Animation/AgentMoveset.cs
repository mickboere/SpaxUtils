using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// ScriptableObject defining a per-agent (or per-body-type) animation configuration.
	/// Supports template inheritance: assign a base moveset as <see cref="template"/> and only
	/// override the fields that differ. Null reference fields and -1 float fields inherit from template.
	/// Can be injected into PoserNode to override its default serialized values.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(AgentMoveset), menuName = "Animation/" + nameof(AgentMoveset))]
	public class AgentMoveset : ScriptableObject
	{
		// Sentinel for "inherit from template" on float fields.
		// All float fields in this class are inherently positive, so -1 is safe.
		private const float INHERIT = -1f;

		[Header("Template")]
		[SerializeField, Tooltip("Base moveset to inherit from. Null/unset fields fall through to this template.")]
		private AgentMoveset template;

		[Header("Movement Blend Trees")]
		[SerializeField] private PoseBlendMap groundedBlendTree;
		[SerializeField] private PoseBlendMap targetingBlendTree;
		[SerializeField] private PoseBlendMap flyingBlendTree;
		[SerializeField] private PoseBlendMap slidingBlendTree;

		[Header("Idle Overrides (replace idle entry in grounded blend tree at runtime)")]
		[SerializeField, Tooltip("Idle pose sequence for passive mode. Null = use grounded blend tree's default idle.")]
		private PoseSequence passiveIdle;
		[SerializeField, Tooltip("Idle pose sequence for untargeted combat mode. Null = use grounded blend tree's default idle.")]
		private PoseSequence combatIdle;

		[Header("Timing (set to -1 to inherit from template)")]
		[SerializeField] private float positionBlendSpeed = INHERIT;
		[SerializeField] private float poseTransitionSpeed = INHERIT;
		[SerializeField] private float targetSwitchDuration = INHERIT;

		[Header("Landing (set floats to -1 to inherit from template)")]
		[SerializeField, Tooltip("Single landing pose. Weight is determined by impact severity. Null = inherit or skip.")]
		private PoseSequence landingPose;
		[SerializeField] private float landingMinImpact = INHERIT;
		[SerializeField] private float landingMaxImpact = INHERIT;
		[SerializeField] private float landingHoldDuration = INHERIT;
		[SerializeField] private float landingFadeOut = INHERIT;
		[SerializeField, Range(-1f, 1f)] private float landingControlReduction = INHERIT;

		// --- Resolved properties: self -> template -> hardcoded default ---

		public PoseBlendMap GroundedBlendTree => Resolve(groundedBlendTree, template?.GroundedBlendTree);
		public PoseBlendMap TargetingBlendTree => Resolve(targetingBlendTree, template?.TargetingBlendTree);
		public PoseBlendMap FlyingBlendTree => Resolve(flyingBlendTree, template?.FlyingBlendTree);
		public PoseBlendMap SlidingBlendTree => Resolve(slidingBlendTree, template?.SlidingBlendTree);

		public PoseSequence PassiveIdle => Resolve(passiveIdle, template?.PassiveIdle);
		public PoseSequence CombatIdle => Resolve(combatIdle, template?.CombatIdle);

		public float PositionBlendSpeed => ResolveFloat(positionBlendSpeed, template?.PositionBlendSpeed, 10f);
		public float PoseTransitionSpeed => ResolveFloat(poseTransitionSpeed, template?.PoseTransitionSpeed, 10f);
		public float TargetSwitchDuration => ResolveFloat(targetSwitchDuration, template?.TargetSwitchDuration, 1f);

		public PoseSequence LandingPose => Resolve(landingPose, template?.LandingPose);
		public float LandingMinImpact => ResolveFloat(landingMinImpact, template?.LandingMinImpact, 5f);
		public float LandingMaxImpact => ResolveFloat(landingMaxImpact, template?.LandingMaxImpact, 20f);
		public float LandingHoldDuration => ResolveFloat(landingHoldDuration, template?.LandingHoldDuration, 0.5f);
		public float LandingFadeOut => ResolveFloat(landingFadeOut, template?.LandingFadeOut, 0.3f);
		public float LandingControlReduction => ResolveFloat(landingControlReduction, template?.LandingControlReduction, 1f);

		private static T Resolve<T>(T self, T fallback) where T : Object
		{
			return self != null ? self : fallback;
		}

		private static float ResolveFloat(float self, float? templateValue, float defaultValue)
		{
			if (self >= 0f)
			{
				return self;
			}
			if (templateValue.HasValue && templateValue.Value >= 0f)
			{
				return templateValue.Value;
			}
			return defaultValue;
		}
	}
}
