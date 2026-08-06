using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Very basic <see cref="IPose"/> implementation for runtime generation.
	/// </summary>
	public class Pose : IPose
	{
		public AnimationClip Clip { get; }

		public bool Mirror { get; }

		public float Duration { get; }

		public ILabeledDataProvider Data { get; }

		/// <summary>Straight line, matching this implementation's linear <see cref="EvaluateTransition"/>.</summary>
		public AnimationCurve TransitionCurve => linear;

		public float ElementWeight => Duration;

		private static readonly AnimationCurve linear = AnimationCurve.Linear(0f, 0f, 1f, 1f);

		public Pose(AnimationClip clip, bool mirror = false, float duration = 1f, ILabeledDataProvider data = null)
		{
			Clip = clip;
			Mirror = mirror;
			Duration = duration;
			Data = data;
		}

		public float EvaluateTransition(float x)
		{
			return Mathf.Clamp01(x);
		}
	}
}
