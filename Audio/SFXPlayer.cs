using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// The one place a clip reaches an <see cref="AudioSourceWrapper"/>;
	/// shared by <see cref="SFXData"/> and <see cref="TieredSFX"/>.
	/// </summary>
	public static class SFXPlayer
	{
		/// <summary>
		/// Returns a random clip from <paramref name="clips"/>, never the one <paramref name="lastClip"/> holds.
		/// </summary>
		public static AudioClip PickClip(IReadOnlyList<AudioClip> clips, ref int lastClip)
		{
			if (clips == null || clips.Count == 0)
			{
				return null;
			}

			if (clips.Count == 1)
			{
				lastClip = 0;
				return clips[0];
			}

			// Prevent clip repetition.
			int i;
			do { i = Random.Range(0, clips.Count); }
			while (i == lastClip);

			lastClip = i;
			return clips[i];
		}

		/// <summary>Plays <paramref name="clip"/> once, cutting off whatever the wrapper was playing.</summary>
		public static void Play(AudioSourceWrapper wrapper, AudioClip clip, float volume, float pitch,
			float minDistance, float maxDistance)
		{
			if (wrapper == null || clip == null)
			{
				return;
			}

			Setup(wrapper, clip, volume, pitch, minDistance, maxDistance, false);
			wrapper.Play();
		}

		/// <summary>Loops <paramref name="clip"/>, optionally entering at a random point in it.</summary>
		public static void PlayLoop(AudioSourceWrapper wrapper, AudioClip clip, float volume, float pitch,
			float minDistance, float maxDistance, bool randomStart = true)
		{
			if (wrapper == null || clip == null)
			{
				return;
			}

			Setup(wrapper, clip, volume, pitch, minDistance, maxDistance, true);

			if (randomStart)
			{
				wrapper.Time = Random.value * wrapper.Duration;
			}

			wrapper.Play();
		}

		private static void Setup(AudioSourceWrapper wrapper, AudioClip clip, float volume, float pitch,
			float minDistance, float maxDistance, bool loop)
		{
			wrapper.Stop();
			wrapper.Clip = clip;
			wrapper.Loop = loop;

			wrapper.AudioSource.pitch = pitch;
			wrapper.Pitch.BaseValue = pitch;

			wrapper.AudioSource.volume = volume;
			wrapper.Volume.BaseValue = volume;

			wrapper.MinDistance = minDistance;
			wrapper.MaxDistance = maxDistance;
		}
	}
}
