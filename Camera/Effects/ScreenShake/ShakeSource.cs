using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Basic <see cref="IShakeSource"/> implementation with animation curve intensity support
	/// that automatically completes after a set duration has elapsed.
	/// </summary>
	public class ShakeSource : IShakeSource
	{
		public int Seed { get; private set; }
		public Vector3 Magnitude { get; private set; }
		public Vector3 Direction { get; private set; }
		public float Time => timer.Time * Frequency;
		public bool Completed => timer.Expired;

		public float Frequency { get; private set; }
		public float Progress => timer.Progress;

		private TimerStruct timer;
		private AnimationCurve falloff;

		public ShakeSource(Vector3 magnitude, Vector3 direction, float duration, float frequency = IShakeSource.DEFAULT_FREQUENCY, AnimationCurve falloff = null)
		{
			Seed = Random.Range(int.MinValue, int.MaxValue);
			Magnitude = magnitude;
			Direction = direction;
			Frequency = frequency;
			timer = new TimerStruct(duration);
			this.falloff = falloff;
		}

		public void Dispose()
		{
		}

		public float EvaluateIntensity()
		{
			return falloff == null ? Progress : falloff.Evaluate(Progress);
		}
	}
}
