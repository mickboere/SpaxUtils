using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Basic <see cref="IShakeSource"/> implementation with animation curve intensity support.
	/// </summary>
	public class ShakeSource : IShakeSource
	{
		public Vector3 Magnitude { get; private set; }
		public float Frequency { get; private set; }
		public float Progress => timer.Progress;

		private TimerStruct timer;
		private AnimationCurve intensity;

		public ShakeSource(Vector3 magnitude, float frequency, float duration, AnimationCurve intensity = null)
		{
			Magnitude = magnitude;
			Frequency = frequency;
			timer = new TimerStruct(duration);
			this.intensity = intensity;
		}

		public void Dispose()
		{
		}

		public float Evaluate()
		{
			return intensity == null ? Progress : intensity.Evaluate(Progress);
		}
	}
}
