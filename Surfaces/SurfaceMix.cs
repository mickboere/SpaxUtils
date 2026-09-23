using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Builds the surface mixes a struck or striking body is made of, weighted by equipment coverage.
	/// </summary>
	public static class SurfaceMix
	{
		/// <summary>
		/// Layers here are different materials at once, so their transients never align; see
		/// <see cref="AudioMixUtils"/>. Verify by ear under Tools > Audio Mix Tester.
		/// </summary>
		public const float EXPONENT = 6f;

		/// <summary>
		/// Surface of whatever covers <paramref name="location"/>, <paramref name="outer"/> winning,
		/// else the bare body's.
		/// </summary>
		public static string Covering(IReadOnlyCollection<RuntimeEquipedData> equiped, string location,
			string outer, string bodySurface)
		{
			if (equiped == null || string.IsNullOrEmpty(location))
			{
				return bodySurface;
			}

			string inner = null;

			foreach (RuntimeEquipedData item in equiped)
			{
				IEquipmentData data = item?.EquipmentData;
				if (data == null || string.IsNullOrEmpty(data.Surface))
				{
					continue;
				}

				if (data.CoversLocations.Contains(outer))
				{
					return data.Surface;
				}

				if (inner == null && data.CoversLocations.Contains(location))
				{
					inner = data.Surface;
				}
			}

			return inner ?? bodySurface;
		}

		/// <summary>
		/// Equipment at its <see cref="IEquipmentData.Coverage"/> plus <paramref name="bodySurface"/> for
		/// whatever is left, which share <c>1 - guardFraction</c> with <paramref name="guardSurface"/> at <paramref name="guardFraction"/>.
		/// </summary>
		public static Dictionary<SurfaceConfiguration, float> BuildBodyMix(SurfaceLibrary library,
			IReadOnlyCollection<RuntimeEquipedData> equiped, string bodySurface,
			string guardSurface = null, float guardFraction = 0f, float intensity = 1f)
		{
			Dictionary<SurfaceConfiguration, float> weights = new Dictionary<SurfaceConfiguration, float>();

			if (library == null || intensity <= 0f)
			{
				return weights;
			}

			SurfaceConfiguration guard = null;
			bool guarding = guardFraction > 0f && !string.IsNullOrEmpty(guardSurface) &&
				library.TryGet(guardSurface, out guard);
			float bodyShare = guarding ? 1f - guardFraction : 1f;

			float covered = 0f;

			if (equiped != null)
			{
				foreach (RuntimeEquipedData item in equiped)
				{
					IEquipmentData data = item?.EquipmentData;
					if (data == null || string.IsNullOrEmpty(data.Surface))
					{
						continue;
					}

					float coverage = Mathf.Clamp01(data.Coverage);
					if (coverage <= 0f || !library.TryGet(data.Surface, out SurfaceConfiguration config))
					{
						continue;
					}

					covered += coverage;
					float share = coverage * bodyShare;
					weights[config] = weights.TryGetValue(config, out float existing) ? existing + share : share;
				}
			}

			// Whatever nothing covers is bare body; past full coverage there is none left to hear.
			float bare = Mathf.Max(0f, 1f - covered) * bodyShare;
			if (bare > 0f && !string.IsNullOrEmpty(bodySurface) &&
				library.TryGet(bodySurface, out SurfaceConfiguration body))
			{
				weights[body] = weights.TryGetValue(body, out float existing) ? existing + bare : bare;
			}

			if (guarding)
			{
				weights[guard] = weights.TryGetValue(guard, out float merged) ? merged + guardFraction : guardFraction;
			}

			return AudioMixUtils.NormalizedMix(weights, intensity, EXPONENT);
		}
	}
}
