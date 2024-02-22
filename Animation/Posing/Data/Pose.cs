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

		public float ElementWeight => Duration;

		public Pose(AnimationClip clip, bool mirror = false, float duration = 1f, ILabeledDataProvider data = null)
		{
			Clip = clip;
			Mirror = mirror;
			Duration = duration;
			Data = data;
		}

		public float EvaluateTransition(float x)
		{
			return x;
		}
	}
}
