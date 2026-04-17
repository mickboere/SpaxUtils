using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Continuous <see cref="IShakeSource"/> implementation that requires manual completion.
	/// </summary>
	public class ContinuousShakeSource : IShakeSource
	{
		/// <inheritdoc/>
		public int Seed { get; private set; }

		/// <inheritdoc/>
		public Vector3 Magnitude { get; set; }

		/// <inheritdoc/>
		public Vector3 Direction { get; set; }

		/// <inheritdoc/>
		public float Time => timer.Time;

		/// <inheritdoc/>
		public bool Completed { get; set; }

		public float Frequency { get; set; }
		public float Intensity { get; set; }

		private TimerClass timer;

		/// <summary>
		/// Creates a new continuous shake source which relies on external manipulation of variables and manual completion.
		/// For a single-shot shake source, use <see cref="ShakeSource"/> instead.
		/// </summary>
		/// <param name="magnitude">X=horizontal bias, Y=vertical bias, Z=overall shake. Set as ZERO to use default values.</param>
		/// <param name="direction">The world direction of the impact. Set as ZERO if direction is already encoded in <paramref name="magnitude"/>.</param>
		/// <param name="frequency">The time speed multiplier defining shaking frequency.</param>
		/// <param name="intensity">The final multiplier for the shake, must be animated manually after creation.</param>
		public ContinuousShakeSource(Vector3 magnitude, Vector3 direction,
			float frequency = IShakeSource.DEFAULT_FREQUENCY, float intensity = 1f)
		{
			Seed = Random.Range(int.MinValue, int.MaxValue);
			Magnitude = magnitude;
			Direction = direction;
			Frequency = frequency;
			Intensity = intensity;
			timer = new TimerClass(null, () => Frequency, true);
		}

		public void Dispose()
		{
			Completed = true;
			timer.Dispose();
		}

		public float EvaluateIntensity()
		{
			return Intensity;
		}
	}
}
