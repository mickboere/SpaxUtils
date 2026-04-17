using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(EnvironmentAudioSettingsAsset), menuName = "ScriptableObjects/Audio/" + nameof(EnvironmentAudioSettingsAsset))]
	public class EnvironmentAudioSettingsAsset : ScriptableObject, IEnvironmentAudioSettings
	{
		public AudioClip Ambience => ambience;
		public TransitionSettings AmbienceTransition => ambienceTransition;

		public AudioClip Music => music;
		public float Delay => delay;
		public bool Loop => loop;
		public bool RandomStart => randomStart;
		public TransitionSettings MusicTransition => musicTransition;

		public AudioReverbPreset Reverb => reverb;

		[Header("Ambience")]
		[SerializeField] private AudioClip ambience;
		[SerializeField] private TransitionSettings ambienceTransition;
		[Header("Music")]
		[SerializeField] private AudioClip music;
		[SerializeField] private float delay;
		[SerializeField] private bool loop;
		[SerializeField] private bool randomStart;
		[SerializeField] private TransitionSettings musicTransition;
		[Header("Reverb")]
		[SerializeField] private AudioReverbPreset reverb;
	}
}
