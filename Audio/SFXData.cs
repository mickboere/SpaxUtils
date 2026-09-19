using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class SFXData
	{
		public const float DISTANCE_MIN = 1f;
		public const float DISTANCE_MAX = 250f;

		public IReadOnlyList<AudioClip> Clips => clips;
		public AudioClip RandomClip => SFXPlayer.PickClip(clips, ref lastClip);

		public Vector2 VolumeRange => volumeRange;
		public float RandomVolume => UnityEngine.Random.Range(volumeRange.x, volumeRange.y);
		public Vector2 PitchRange => pitchRange;
		public float RandomPitch => UnityEngine.Random.Range(pitchRange.x, pitchRange.y);
		public float Distance => distance;
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
		public void Play(AudioSourceWrapper audioSourceWrapper, float volume = 1f, float pitch = 1f, float distance = 1f)
		{
			PlayClip(audioSourceWrapper, RandomVolume * volume, RandomPitch * pitch, distance);
		}

		private void PlayClip(AudioSourceWrapper audioSourceWrapper, float volume, float pitch, float distance)
		{
			SFXPlayer.Play(audioSourceWrapper, RandomClip, volume, pitch,
				MinDistance * distance, MaxDistance * distance);
		}

		public void PlayLoop(AudioSourceWrapper audioSourceWrapper, bool randomStart = false, float pitch = 1f, float volume = 1f, float distance = 1f)
		{
			SFXPlayer.PlayLoop(audioSourceWrapper, RandomClip, volume, pitch,
				MinDistance * distance, MaxDistance * distance, randomStart);
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
				//SpaxDebug.Warning("No clips defined.");
				return;
			}

			audioSourceWrapper.PlayOneShot(RandomClip, RandomVolume * volume);
		}
	}
}
