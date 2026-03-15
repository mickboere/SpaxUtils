using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Configurable asset defining an animated action that can be performed at a POI.
	/// The pose-to-pose or animator-driven equivalent of <see cref="PerformanceMove"/>,
	/// stripped of charge/followup/cost mechanics.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(AnimatedAction), menuName = "Performance/" + nameof(AnimatedAction))]
	public class AnimatedAction : ScriptableObject
	{
		/// <summary>
		/// Act identifier string.
		/// </summary>
		public string Identifier => identifier;

		/// <summary>
		/// Whether the action loops indefinitely until cancelled.
		/// FALSE: plays through once for <see cref="Duration"/> then finishes.
		/// </summary>
		public bool Prolonged => prolonged;

		/// <summary>
		/// Time in seconds for the pose weight to ramp from 0 to 1 on entry.
		/// </summary>
		public float FadeInDuration => fadeInDuration;

		/// <summary>
		/// Duration of the performing phase. Only used when <see cref="Prolonged"/> is FALSE.
		/// When 0, the duration is inferred from the <see cref="PosingData"/> (if available).
		/// </summary>
		public float Duration => duration;

		/// <summary>
		/// Time in seconds for the pose weight to ramp from 1 to 0 on exit or cancel.
		/// </summary>
		public float FadeOutDuration => fadeOutDuration;

		/// <summary>
		/// Whether animation is driven by the poser system or by animator parameters.
		/// </summary>
		public PerformanceAnimationType AnimationType => animationType;

		/// <summary>
		/// Animator action index, used when <see cref="AnimationType"/> is <see cref="PerformanceAnimationType.Animator"/>.
		/// </summary>
		public int AnimationIndex => animationIndex;

		/// <summary>
		/// Posing data (e.g. <see cref="PoseSequence"/>), used when <see cref="AnimationType"/> is <see cref="PerformanceAnimationType.Poser"/>.
		/// </summary>
		public PosingData PosingData => posingData;

		[SerializeField, ConstDropdown(typeof(IActIdentifiers))] private string identifier;
		[SerializeField] private bool prolonged = true;
		[SerializeField, Conditional(nameof(prolonged)), Tooltip("Only used when not prolonged. 0 = infer from posing data.")] private float duration;
		[SerializeField] private float fadeInDuration = 0.3f;
		[SerializeField] private float fadeOutDuration = 0.3f;

		[Header("Animation")]
		[SerializeField] private PerformanceAnimationType animationType;
		[SerializeField, Conditional(nameof(animationType), 0)] private int animationIndex;
		[SerializeField, Conditional(nameof(animationType), 1)] private PosingData posingData;

		/// <summary>
		/// Returns the effective performing duration.
		/// For prolonged actions this returns 0 (indefinite).
		/// For non-prolonged actions this returns <see cref="Duration"/>,
		/// or the posing data's natural duration if Duration is 0.
		/// </summary>
		public float GetEffectiveDuration()
		{
			if (Prolonged)
			{
				return 0f;
			}

			if (duration > 0f)
			{
				return duration;
			}

			if (animationType == PerformanceAnimationType.Poser && posingData is IPoseSequence sequence)
			{
				return sequence.TotalDuration;
			}

			return 0f;
		}

		public override string ToString()
		{
			return $"AnimatedAction(\"{identifier}\", prolonged:{prolonged}, animType:{animationType})";
		}
	}
}
