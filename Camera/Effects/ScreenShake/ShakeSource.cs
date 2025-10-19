using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Basic <see cref="IShakeSource"/> implementation with animation curve intensity support.
	/// </summary>
	public class ShakeSource : IShakeSource
	{
		public int Seed { get; private set; }
		public Vector3 Magnitude { get; private set; }
		public float Frequency { get; private set; }
		public float Time => timer.Time;
		public float Progress => timer.Progress;

		private TimerStruct timer;
		private AnimationCurve falloff;

		public ShakeSource(Vector3 magnitude, float frequency, float duration, AnimationCurve falloff = null)
		{
			Seed = Random.Range(int.MinValue, int.MaxValue);
			Magnitude = magnitude;
			Frequency = frequency;
			timer = new TimerStruct(duration);
			this.falloff = falloff;
		}

		public void Dispose()
		{
		}

		public float Evaluate()
		{
			return falloff == null ? Progress : falloff.Evaluate(Progress);
		}
	}
}
