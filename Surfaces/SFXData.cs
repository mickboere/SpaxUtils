using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class SFXData
	{
		public IReadOnlyList<AudioClip> Clips => clips;
		public AudioClip RandomClip => clips[UnityEngine.Random.Range(0, clips.Count)];

		public Vector2 VolumeRange => volumeRange;
		public float RandomVolume => UnityEngine.Random.Range(volumeRange.x, volumeRange.y);
		public Vector2 PitchRange => pitchRange;
		public float RandomPitch => UnityEngine.Random.Range(pitchRange.x, pitchRange.y);

		[SerializeField] private List<AudioClip> clips;
		[SerializeField, MinMaxRange(0.01f, 1f, true)] private Vector2 volumeRange = new Vector2(1f, 1f);
		[SerializeField, MinMaxRange(0.01f, 3f, true, false)] private Vector2 pitchRange = new Vector2(1f, 1f);

		public void Play(AudioSource audioSource, float volume = 1f)
		{
			if (clips == null || clips.Count == 0)
			{
				SpaxDebug.Warning("No clips defined.");
				return;
			}

			audioSource.pitch = RandomPitch;
			audioSource.volume = RandomVolume * volume;
			audioSource.clip = RandomClip;
			audioSource.Play();
		}

		public void PlayOneShot(AudioSource audioSource, float volume = 1f)
		{
			if (clips == null || clips.Count == 0)
			{
				SpaxDebug.Warning("No clips defined.");
				return;
			}

			audioSource.pitch = RandomPitch;
			audioSource.PlayOneShot(RandomClip, RandomVolume * volume);
		}
	}
}
