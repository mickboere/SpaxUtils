using UnityEngine;

namespace SpaxUtils
{
	[RequireComponent(typeof(AudioSource))]
	public class AudioSourceWrapper : MonoBehaviour
	{
		public AudioSource AudioSource
		{
			get
			{
				EnsureAudioSource();
				return audioSource;
			}
		}

		#region Property Wrappers

		public AudioClip Clip { get { return AudioSource.clip; } set { AudioSource.clip = value; } }
		public bool Mute { get { return AudioSource.mute; } set { audioSource.mute = value; } }
		public bool BypassEffects { get { return AudioSource.bypassEffects; } set { audioSource.bypassEffects = value; } }
		public bool BypassListenerEffects { get { return AudioSource.bypassListenerEffects; } set { audioSource.bypassListenerEffects = value; } }
		public bool BypassReverbZones { get { return AudioSource.bypassReverbZones; } set { audioSource.bypassReverbZones = value; } }
		public bool PlayOnAwake { get { return AudioSource.playOnAwake; } set { audioSource.playOnAwake = value; } }
		public bool Loop { get { return AudioSource.loop; } set { audioSource.loop = value; } }
		public int Priority { get { return AudioSource.priority; } set { audioSource.priority = value; } }
		public CompositeFloat Volume { get; } = new CompositeFloat(1f);
		public CompositeFloat Pitch { get; } = new CompositeFloat(1f);
		public float StereoPan { get { return AudioSource.panStereo; } set { audioSource.panStereo = value; } }
		public float SpatialBlend { get { return AudioSource.spatialBlend; } set { audioSource.spatialBlend = value; } }
		public float ReverbZoneMix { get { return AudioSource.reverbZoneMix; } set { audioSource.reverbZoneMix = value; } }

		public float Time { get { return AudioSource.time; } set { audioSource.time = value; } }
		public float Duration { get { return AudioSource.clip.length; } }
		public bool IsPlaying => AudioSource.isPlaying;
		public float MinDistance { get { return AudioSource.minDistance; } set { audioSource.minDistance = value; } }
		public float MaxDistance { get { return AudioSource.maxDistance; } set { audioSource.maxDistance = value; } }

		#endregion Property Wrappers

		[SerializeField] private AudioSource audioSource;

		private TimerClass timer;
		private FloatFuncModifier mod;

		protected void Awake()
		{
			EnsureAudioSource();
		}

		protected void Update()
		{
			audioSource.pitch = Pitch.Value;
			audioSource.volume = Volume.Value;

			if (timer != null && timer.Update(UnityEngine.Time.deltaTime))
			{
				Cleanup();
				Stop();
			}
		}

		#region Method Wrappers

		public void PlayOneShot(AudioClip audioClip, float volumeScale = 1f)
		{
			audioSource.PlayOneShot(audioClip, volumeScale);
		}

		public void Play()
		{
			audioSource.Play();
		}

		public void Pause()
		{
			audioSource.Pause();
			if (timer != null)
			{
				timer.Pause();
			}
		}

		public void Continue()
		{
			audioSource.UnPause();
			if (timer != null)
			{
				timer.Continue();
			}
		}

		public void Stop()
		{
			audioSource.Stop();
			Cleanup();
		}

		#endregion Method Wrappers

		#region Fading Methods

		public void FadeIn(float duration, EasingMethod easing = EasingMethod.Linear)
		{
			Cleanup();
			timer = new TimerClass(duration, 1f, false);
			mod = new FloatFuncModifier(ModMethod.Absolute, (float v) => v * timer.Progress.Ease(easing));
			Volume.AddModifier(mod);
		}

		public void FadeOut(float duration, EasingMethod easing = EasingMethod.Linear)
		{
			Cleanup();
			timer = new TimerClass(duration, 1f, false);
			mod = new FloatFuncModifier(ModMethod.Absolute, (float v) => v * timer.Progress.Invert().Ease(easing));
			Volume.AddModifier(mod);
		}

		#endregion Fading Methods

		private AudioSource EnsureAudioSource()
		{
			if (!audioSource) audioSource = GetComponent<AudioSource>();
			if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
			return audioSource;
		}

		private void Cleanup()
		{
			if (timer != null)
			{
				timer.Dispose();
				timer = null;
			}

			if (mod != null)
			{
				Volume.RemoveModifier(mod);
				mod?.Dispose();
				mod = null;
			}
		}
	}
}
