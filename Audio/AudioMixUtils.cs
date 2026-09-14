using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Utilities for mixing simultaneous sounds so their sum is perceived as loud as <c>intensity</c> demands.
	/// </summary>
	public static class AudioMixUtils
	{
		/// <summary>Power sum; correct for steady uncorrelated signal, too quiet for layered transients.</summary>
		public const float EQUAL_POWER = 2f;

		/// <summary>High enough that the loudest layer plays at very nearly full volume.</summary>
		public const float DOMINANT = 12f;

		/// <summary>A weight is a share of ENERGY, so half the weight is -3dB. What a "percentage of the mix" means.</summary>
		public const float ENERGY_SHARE = 0.5f;

		/// <summary>A weight is a share of AMPLITUDE, so half the weight is -6dB and quiet layers all but vanish.</summary>
		public const float AMPLITUDE_SHARE = 1f;

		/// <summary>
		/// Converts <paramref name="weights"/> into volumes summing to <paramref name="intensity"/> under the
		/// <paramref name="exponent"/>-norm. Higher exponent = louder layers; see <see cref="EQUAL_POWER"/>/<see cref="DOMINANT"/>.
		/// </summary>
		/// <param name="share">What a weight is a share OF; see <see cref="ENERGY_SHARE"/>/<see cref="AMPLITUDE_SHARE"/>.</param>
		public static Dictionary<T, float> NormalizedMix<T>(IReadOnlyDictionary<T, float> weights,
			float intensity = 1f, float exponent = EQUAL_POWER, float share = ENERGY_SHARE)
		{
			Dictionary<T, float> mix = new Dictionary<T, float>();

			if (weights == null || weights.Count == 0 || intensity <= 0f)
			{
				return mix;
			}

			exponent = Mathf.Max(1f, exponent);
			share = Mathf.Clamp(share, 0.1f, 1f);

			float sum = 0f;
			foreach (KeyValuePair<T, float> weight in weights)
			{
				sum += Mathf.Pow(Mathf.Pow(Mathf.Max(0f, weight.Value), share), exponent);
			}

			float normalization = sum > 0.0001f ? 1f / Mathf.Pow(sum, 1f / exponent) : 1f;

			foreach (KeyValuePair<T, float> weight in weights)
			{
				float value = Mathf.Max(0f, weight.Value);
				if (value <= 0f)
				{
					continue;
				}

				mix[weight.Key] = Mathf.Pow(value, share) * intensity * normalization;
			}

			return mix;
		}
	}
}
