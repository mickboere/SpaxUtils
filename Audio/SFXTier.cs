using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// One rung of a <see cref="TieredSFX"/> ladder: the clips that speak for a given intensity.
	/// </summary>
	[Serializable]
	public class SFXTier
	{
		public float Intensity => intensity;
		public IReadOnlyList<AudioClip> Clips => clips;
		public bool HasClips => clips != null && clips.Count > 0;

		[SerializeField, Range(0f, 1f)] private float intensity;
		[SerializeField] private List<AudioClip> clips = new List<AudioClip>();
		[SerializeField] private bool overrideSettings;
		[SerializeField] private SFXSettings settings = new SFXSettings();

		[NonSerialized] private int lastClip = -1;

		/// <summary>Returns a random clip from this tier, never the one it played last.</summary>
		public AudioClip NextClip()
		{
			return SFXPlayer.PickClip(clips, ref lastClip);
		}

		/// <summary>The settings this tier plays under, falling back to <paramref name="defaults"/>.</summary>
		public SFXSettings Resolve(SFXSettings defaults)
		{
			return overrideSettings && settings != null ? settings : defaults;
		}
	}
}
