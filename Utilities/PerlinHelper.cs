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

		[NonSerialized] private Func<float> polarizationProvider; // when set, sampled live each Update instead of 'polarization'.
		[NonSerialized] private Func<float> frequencyProvider; // when set, sampled live each Update instead of 'frequency'.

		public PerlinHelper(float polarization, float frequency, float min = 0f, float max = 1f, float time = -1f)
		{
			this.polarization = polarization;
			this.frequency = frequency;

			Initialize(time, min, max);
		}

		/// <summary>Dynamic-frequency variant: <paramref name="frequencyProvider"/> yields the final frequency each Update.</summary>
		public PerlinHelper(float polarization, Func<float> frequencyProvider, float min = 0f, float max = 1f, float time = -1f)
		{
			this.polarization = polarization;
			this.frequencyProvider = frequencyProvider;

			Initialize(time, min, max);
		}

		/// <summary>Fully dynamic variant: both providers are sampled live each Update (polarization + frequency).</summary>
		public PerlinHelper(Func<float> polarizationProvider, Func<float> frequencyProvider, float min = 0f, float max = 1f, float time = -1f)
		{
			this.polarizationProvider = polarizationProvider;
			this.frequencyProvider = frequencyProvider;

			Initialize(time, min, max);
		}

		public PerlinHelper(PerlinHelperSettings settings, float tPol, float tFreq, float min = 0f, float max = 1f, float t = -1f)
			: this(settings.Polarization.Lerp(tPol), settings.Frequency.Lerp(tFreq), min, max, t) { }

		/// <summary>Dynamic-frequency variant: <paramref name="tFreqProvider"/> yields a 0-1 t sampled live, lerped through the settings range.</summary>
		public PerlinHelper(PerlinHelperSettings settings, float tPol, Func<float> tFreqProvider, float min = 0f, float max = 1f, float t = -1f)
			: this(settings.Polarization.Lerp(tPol), () => settings.Frequency.Lerp(tFreqProvider()), min, max, t) { }

		/// <summary>Fully dynamic variant: both t-providers yield a 0-1 t sampled live, lerped through the settings polarization/frequency ranges.</summary>
		public PerlinHelper(PerlinHelperSettings settings, Func<float> tPolProvider, Func<float> tFreqProvider, float min = 0f, float max = 1f, float t = -1f)
			: this(() => settings.Polarization.Lerp(tPolProvider()), () => settings.Frequency.Lerp(tFreqProvider()), min, max, t) { }

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
			Time += (frequencyProvider != null ? frequencyProvider() : frequency) * delta;
			float pol = polarizationProvider != null ? polarizationProvider() : polarization;
			float perlin = Mathf.PerlinNoise1D(Time);
			return (perlin.Remap(-1f, 1f) * pol).Clamp(-1f, 1f).Remap(Min, Max, -1, 1f);
		}
	}
}
