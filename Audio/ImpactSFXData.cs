using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class ImpactSFXData
	{
		[field: SerializeField, Range(0f, 1f)] public float Intensity { get; private set; }
		[field: SerializeField] public SFXData SFX { get; private set; }

		/// <summary>
		/// Returns the SFX of the loudest gradation <paramref name="entries"/> holds at or below <paramref name="intensity"/>,
		/// falling back to the softest authored one. Null when nothing is authored.
		/// </summary>
		public static SFXData Select(IReadOnlyList<ImpactSFXData> entries, float intensity)
		{
			if (entries == null)
			{
				return null;
			}

			ImpactSFXData match = null;
			ImpactSFXData fallback = null;

			for (int i = 0; i < entries.Count; i++)
			{
				ImpactSFXData entry = entries[i];
				if (entry == null || entry.SFX == null || entry.SFX.Clips == null || entry.SFX.Clips.Count == 0)
				{
					continue;
				}

				if (fallback == null || entry.Intensity < fallback.Intensity)
				{
					fallback = entry;
				}

				if (entry.Intensity <= intensity && (match == null || entry.Intensity > match.Intensity))
				{
					match = entry;
				}
			}

			return match != null ? match.SFX : fallback?.SFX;
		}
	}
}
