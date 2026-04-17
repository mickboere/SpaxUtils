using UnityEngine;

namespace SpaxUtils
{
	public interface IEnvironmentAudioSettings
	{
		AudioClip Ambience { get; }
		TransitionSettings AmbienceTransition { get; }

		AudioClip Music { get; }
		float Delay { get; }
		bool Loop { get; }
		bool RandomStart { get; }
		TransitionSettings MusicTransition { get; }

		AudioReverbPreset Reverb{ get; }
	}
}
