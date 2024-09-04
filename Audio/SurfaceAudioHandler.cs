using UnityEngine;

namespace SpaxUtils
{
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
