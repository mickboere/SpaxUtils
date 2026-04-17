using UnityEngine;
using System;

namespace SpaxUtils
{
	/// <summary>
	/// Helper class for timed-based perlin noise.
	/// </summary>
	[Serializable]
	public class PerlinHelper : IDisposable
	{
		[NonSerialized] public float Time;
		[NonSerialized] public float Min;
		[NonSerialized] public float Max;

		[SerializeField] public float polarization;
		[SerializeField] public float frequency;

		public PerlinHelper(float polarization, float frequency, float min = 0f, float max = 1f, float time = -1f)
		{
			this.polarization = polarization;
			this.frequency = frequency;

			Initialize(time, min, max);
		}

		public PerlinHelper(PerlinHelperSettings settings, float tPol, float tFreq, float min = 0f, float max = 1f, float t = -1f)
			: this(settings.Polarization.Lerp(tPol), settings.Frequency.Lerp(tFreq), min, max, t) { }

		public void Initialize(float time = -1f, float min = 0f, float max = 1f)
		{
			if (time == -1f)
			{
				Time = UnityEngine.Random.value * 1000f;
			}
			else
			{
				Time = time;
			}
			Min = min;
			Max = max;
		}

		public void Dispose() { }

		public float Update(float delta)
		{
			Time += frequency * delta;
			float perlin = Mathf.PerlinNoise1D(Time);
			return (perlin.Remap(-1f, 1f) * polarization).Clamp(-1f, 1f).Remap(Min, Max, -1, 1f);
		}
	}
}
