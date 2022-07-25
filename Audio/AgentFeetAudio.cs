using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentFeetAudio : EntityComponentBase
	{
		[SerializeField] private AudioSource feetAudioSource;

		private SurfaceLibrary surfaceLibrary;
		private ILegsComponent legs;

		public void InjectDependencies(SurfaceLibrary surfaceLibrary, ILegsComponent legs)
		{
			this.legs = legs;
			this.surfaceLibrary = surfaceLibrary;
		}

		protected void OnEnable()
		{
			legs.FootstepEvent += OnFootstepEvent;
		}

		protected void OnDisable()
		{
			legs.FootstepEvent -= OnFootstepEvent;
		}

		private void OnFootstepEvent(ILeg leg, bool grounded)
		{
			if (!grounded)
			{
				return;
			}

			if (SurfaceComponent.TryGetSurfaceValues(leg.GroundedHit, out Dictionary<string, float> surfaces)
				&& surfaces != null && surfaces.Count > 0)
			{
				foreach (KeyValuePair<string, float> surface in surfaces)
				{
					SurfaceConfiguration config = surfaceLibrary.Get(surface.Key);
					if (config != null)
					{
						feetAudioSource.pitch = config.Audio.Light.RandomPitch;
						feetAudioSource.PlayOneShot(config.Audio.Light.RandomClip, config.Audio.Light.RandomVolume);
					}
				}
			}
			else
			{
				SurfaceConfiguration config = surfaceLibrary.Get(DefaultSurfaceTypes.DEFAULT);
				feetAudioSource.PlayOneShot(config.Audio.Light.RandomClip);
			}
		}
	}

	public class SurfaceAudioHandler
	{
		private const float LEVEL_TRESHOLD = 0.025f;

		private AudioSource source;

		public SurfaceAudioHandler(AudioSource source, AudioClip clip)
		{
			this.source = source;
			this.source.loop = true;
			this.source.clip = clip;
		}

		public void SetLevel(float level, float volumeMultiplier = 1f)
		{
			if (level < LEVEL_TRESHOLD)
			{
				source.Stop();
				return;
			}
			else if (!source.isPlaying)
			{
				source.Play();
			}

			//source.volume = volume.Evaluate(level) * volumeMultiplier;
			//source.pitch = pitch.Evaluate(level);
		}
	}
}
