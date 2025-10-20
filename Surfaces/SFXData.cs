using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class SFXData
	{
		public const float DISTANCE_MIN = 1f;
		public const float DISTANCE_MAX = 100f;

		public IReadOnlyList<AudioClip> Clips => clips;
		public AudioClip RandomClip
		{
			get
			{
				if (clips.Count > 1)
				{
					// Prevent clip repetition.
					int i;
					do { i = UnityEngine.Random.Range(0, clips.Count); }
					while (i == lastClip);
					lastClip = i;
					return clips[i];
				}
				else
				{
					return clips[0];
				}
			}
		}

		public Vector2 VolumeRange => volumeRange;
		public float RandomVolume => UnityEngine.Random.Range(volumeRange.x, volumeRange.y);
		public Vector2 PitchRange => pitchRange;
		public float RandomPitch => UnityEngine.Random.Range(pitchRange.x, pitchRange.y);
		public float MinDistance => distance * DISTANCE_MIN;
		public float MaxDistance => distance * DISTANCE_MAX;

		[SerializeField] private List<AudioClip> clips;
		[SerializeField, MinMaxRange(0.01f, 1f, true)] private Vector2 volumeRange = new Vector2(1f, 1f);
		[SerializeField, MinMaxRange(0.01f, 3f, true, false)] private Vector2 pitchRange = new Vector2(1f, 1f);
		[SerializeField] private float distance = 1f;

		[NonSerialized] private int lastClip = -1;

		/// <summary>
		/// Plays a random clip at random pitch and random volume, within the defined ranges, from <paramref name="audioSourceWrapper"/>.
		/// </summary>
		/// <param name="audioSourceWrapper">The <see cref="AudioSourceWrapper"/> the play the clip from.</param>
		/// <param name="volume">Volume multiplier.</param>
		/// <param name="distance">Distance multiplier.</param>
		public void Play(AudioSourceWrapper audioSourceWrapper, float volume = 1f, float distance = 1f)
		{
			if (clips == null || clips.Count == 0)
			{
				SpaxDebug.Warning("No clips defined.");
				return;
			}

			audioSourceWrapper.Stop();
			audioSourceWrapper.Clip = RandomClip;
			audioSourceWrapper.Loop = false;
			audioSourceWrapper.Pitch.BaseValue = RandomPitch;
			audioSourceWrapper.Volume.BaseValue = RandomVolume * volume;
			audioSourceWrapper.MinDistance = MinDistance * distance;
			audioSourceWrapper.MaxDistance = MaxDistance * distance;
			audioSourceWrapper.Play();
		}

		public void PlayLoop(AudioSourceWrapper audioSourceWrapper, bool randomStart = false)
		{
			audioSourceWrapper.Stop();
			audioSourceWrapper.Clip = RandomClip;
			audioSourceWrapper.Loop = true;
			audioSourceWrapper.Pitch.BaseValue = 1f;
			audioSourceWrapper.Volume.BaseValue = 1f;
			audioSourceWrapper.MinDistance = MinDistance;
			audioSourceWrapper.MaxDistance = MaxDistance;
			if (randomStart) audioSourceWrapper.Time = UnityEngine.Random.value * audioSourceWrapper.Duration;
			audioSourceWrapper.Play();
		}

		/// <summary>
		/// Plays a one-shot of a random clip at random volume, within the defined ranges, from <paramref name="audioSourceWrapper"/>.
		/// </summary>
		/// <param name="audioSourceWrapper">The <see cref="AudioSourceWrapper"/> to play the one-shot from.</param>
		/// <param name="volume">Volume multiplier.</param>
		public void PlayOneShot(AudioSourceWrapper audioSourceWrapper, float volume = 1f)
		{
			if (clips == null || clips.Count == 0)
			{
				SpaxDebug.Warning("No clips defined.");
				return;
			}

			audioSourceWrapper.PlayOneShot(RandomClip, RandomVolume * volume);
		}
	}
}
