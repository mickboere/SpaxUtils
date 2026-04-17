using UnityEngine;
using UnityEngine.Audio;

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

		// Clip & Playback
		public AudioClip Clip { get { return AudioSource.clip; } set { AudioSource.clip = value; } }
		public bool Loop { get { return AudioSource.loop; } set { audioSource.loop = value; } }
		public bool PlayOnAwake { get { return AudioSource.playOnAwake; } set { audioSource.playOnAwake = value; } }
		public float Time { get { return AudioSource.time; } set { audioSource.time = value; } }
		public int TimeSamples { get { return AudioSource.timeSamples; } set { audioSource.timeSamples = value; } }
		public float Duration { get { return AudioSource.clip != null ? AudioSource.clip.length : 0f; } }
		public bool IsPlaying => AudioSource.isPlaying;

		// Volume & Pitch
		public CompositeFloat Volume { get; } = new CompositeFloat(1f);
		public CompositeFloat Pitch { get; } = new CompositeFloat(1f);
		public bool Mute { get { return AudioSource.mute; } set { audioSource.mute = value; } }
		public int Priority { get { return AudioSource.priority; } set { audioSource.priority = value; } }

		// Stereo & Spatialization
		public float StereoPan { get { return AudioSource.panStereo; } set { audioSource.panStereo = value; } }
		public float SpatialBlend { get { return AudioSource.spatialBlend; } set { audioSource.spatialBlend = value; } }

		// 3D Sound Settings
		public float DopplerLevel { get { return AudioSource.dopplerLevel; } set { audioSource.dopplerLevel = value; } }
		public float Spread { get { return AudioSource.spread; } set { audioSource.spread = value; } }
		public AudioRolloffMode RolloffMode { get { return AudioSource.rolloffMode; } set { audioSource.rolloffMode = value; } }
		public float MinDistance { get { return AudioSource.minDistance; } set { audioSource.minDistance = value; } }
		public float MaxDistance { get { return AudioSource.maxDistance; } set { audioSource.maxDistance = value; } }

		// Reverb & Effects
		public float ReverbZoneMix { get { return AudioSource.reverbZoneMix; } set { audioSource.reverbZoneMix = value; } }
		public bool BypassEffects { get { return AudioSource.bypassEffects; } set { audioSource.bypassEffects = value; } }
		public bool BypassListenerEffects { get { return AudioSource.bypassListenerEffects; } set { audioSource.bypassListenerEffects = value; } }
		public bool BypassReverbZones { get { return AudioSource.bypassReverbZones; } set { audioSource.bypassReverbZones = value; } }

		// Listener Ignore
		public bool IgnoreListenerVolume { get { return AudioSource.ignoreListenerVolume; } set { audioSource.ignoreListenerVolume = value; } }
		public bool IgnoreListenerPause { get { return AudioSource.ignoreListenerPause; } set { audioSource.ignoreListenerPause = value; } }

		// Velocity & Output
		public AudioVelocityUpdateMode VelocityUpdateMode { get { return AudioSource.velocityUpdateMode; } set { audioSource.velocityUpdateMode = value; } }
		public AudioMixerGroup OutputAudioMixerGroup { get { return AudioSource.outputAudioMixerGroup; } set { audioSource.outputAudioMixerGroup = value; } }

		// Time Scale
		public bool UseScaledTime
		{
			get { return useScaledTime; }
			set
			{
				useScaledTime = value;
				UpdateTimeScaleModifier();
			}
		}

		/// <summary>
		/// The effective time scale applied to pitch. Combines global <see cref="UnityEngine.Time.timeScale"/>
		/// (when <see cref="UseScaledTime"/> is enabled) with the entity time scale (when set via <see cref="SetEntityTimeScale"/>).
		/// </summary>
		public float EffectiveTimeScale
		{
			get
			{
				float scale = 1f;
				if (useScaledTime)
				{
					scale *= UnityEngine.Time.timeScale;
				}
				if (entityTimeScale != null)
				{
					scale *= entityTimeScale.Value;
				}
				return scale;
			}
		}

		#endregion Property Wrappers

		[SerializeField] private AudioSource audioSource;
		[SerializeField] private bool useScaledTime;

		// Fade state
		private TimerClass timer;
		private FloatFuncModifier mod;
		private bool fadingOut;

		// Time scale state
		private EntityStat entityTimeScale;
		private FloatFuncModifier timeScaleMod;

		protected void Awake()
		{
			EnsureAudioSource();
			UpdateTimeScaleModifier();
		}

		protected void Update()
		{
			audioSource.pitch = Pitch.Value;
			audioSource.volume = Volume.Value;

			if (timer != null && timer.Update(UnityEngine.Time.deltaTime))
			{
				Cleanup();
				if (fadingOut)
				{
					Stop();
				}
			}
		}

		#region Method Wrappers

		public void PlayOneShot(AudioClip audioClip, float volumeScale = 1f)
		{
			audioSource.PlayOneShot(audioClip, volumeScale);
		}

		public void Play(AudioClip clip = null)
		{
			if (audioSource.isActiveAndEnabled)
			{
				if (clip != null)
				{
					Clip = clip;
				}
				audioSource.Play();
			}
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
			fadingOut = false;
		}

		public void FadeOut(float duration, EasingMethod easing = EasingMethod.Linear)
		{
			Cleanup();
			timer = new TimerClass(duration, 1f, false);
			mod = new FloatFuncModifier(ModMethod.Absolute, (float v) => v * timer.Progress.Invert().Ease(easing));
			Volume.AddModifier(mod);
			fadingOut = true;
		}

		#endregion Fading Methods

		#region Time Scale

		/// <summary>
		/// Sets an <see cref="EntityStat"/> to use as entity-level time scale modifier on pitch.
		/// Combined with global <see cref="UnityEngine.Time.timeScale"/> when <see cref="UseScaledTime"/> is enabled.
		/// </summary>
		public void SetEntityTimeScale(EntityStat stat)
		{
			entityTimeScale = stat;
			UpdateTimeScaleModifier();
		}

		/// <summary>
		/// Clears the entity-level time scale reference.
		/// </summary>
		public void ClearEntityTimeScale()
		{
			entityTimeScale = null;
			UpdateTimeScaleModifier();
		}

		private void UpdateTimeScaleModifier()
		{
			bool needsMod = useScaledTime || entityTimeScale != null;

			if (needsMod && timeScaleMod == null)
			{
				timeScaleMod = new FloatFuncModifier(ModMethod.Absolute, (float v) => v * EffectiveTimeScale);
				Pitch.AddModifier(timeScaleMod);
			}
			else if (!needsMod && timeScaleMod != null)
			{
				Pitch.RemoveModifier(timeScaleMod);
				timeScaleMod.Dispose();
				timeScaleMod = null;
			}
		}

		#endregion Time Scale

		#region Copy Settings

		/// <summary>
		/// Copies all settings (except clip, playback state, and entity time scale) from another <see cref="AudioSourceWrapper"/>.
		/// </summary>
		public void CopySettings(AudioSourceWrapper source)
		{
			// Time scale (before other settings so modifier is in place).
			UseScaledTime = source.useScaledTime;

			// Volume & Pitch (base values)
			Volume.BaseValue = source.Volume.BaseValue;
			Pitch.BaseValue = source.Pitch.BaseValue;

			CopySharedSettings(source.audioSource);
		}

		/// <summary>
		/// Copies all settings (except clip and playback state) from a raw <see cref="AudioSource"/>.
		/// </summary>
		public void CopySettings(AudioSource source)
		{
			// Volume & Pitch (base values)
			Volume.BaseValue = source.volume;
			Pitch.BaseValue = source.pitch;

			CopySharedSettings(source);
		}

		private void CopySharedSettings(AudioSource source)
		{
			// General
			Mute = source.mute;
			Loop = source.loop;
			PlayOnAwake = source.playOnAwake;
			Priority = source.priority;

			// Stereo & Spatialization
			StereoPan = source.panStereo;
			SpatialBlend = source.spatialBlend;

			// 3D Sound Settings
			DopplerLevel = source.dopplerLevel;
			Spread = source.spread;
			RolloffMode = source.rolloffMode;
			MinDistance = source.minDistance;
			MaxDistance = source.maxDistance;

			// Reverb & Effects
			ReverbZoneMix = source.reverbZoneMix;
			BypassEffects = source.bypassEffects;
			BypassListenerEffects = source.bypassListenerEffects;
			BypassReverbZones = source.bypassReverbZones;

			// Listener Ignore
			IgnoreListenerVolume = source.ignoreListenerVolume;
			IgnoreListenerPause = source.ignoreListenerPause;

			// Velocity & Output
			VelocityUpdateMode = source.velocityUpdateMode;
			OutputAudioMixerGroup = source.outputAudioMixerGroup;

			// Custom curves
			CopyCurves(source);
		}

		private void CopyCurves(AudioSource source)
		{
			audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, source.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
			audioSource.SetCustomCurve(AudioSourceCurveType.SpatialBlend, source.GetCustomCurve(AudioSourceCurveType.SpatialBlend));
			audioSource.SetCustomCurve(AudioSourceCurveType.Spread, source.GetCustomCurve(AudioSourceCurveType.Spread));
			audioSource.SetCustomCurve(AudioSourceCurveType.ReverbZoneMix, source.GetCustomCurve(AudioSourceCurveType.ReverbZoneMix));
		}

		#endregion Copy Settings

		/// <summary>
		/// Resets all values to Unity's default AudioSource values.
		/// </summary>
		public void Reset()
		{
			Stop();

			Pitch.ClearModifiers();
			Volume.ClearModifiers();

			// Clear time scale references (modifiers already removed by ClearModifiers).
			timeScaleMod = null;
			entityTimeScale = null;
			useScaledTime = false;

			// Clip
			Clip = null;

			// Volume & Pitch
			Volume.BaseValue = 1f;
			Pitch.BaseValue = 1f;
			Mute = false;
			Priority = 128;

			// General
			Loop = false;
			PlayOnAwake = false;

			// Stereo & Spatialization
			StereoPan = 0f;
			SpatialBlend = 1f;

			// 3D Sound Settings
			DopplerLevel = 1f;
			Spread = 0f;
			RolloffMode = AudioRolloffMode.Logarithmic;
			MinDistance = 1f;
			MaxDistance = 500f;

			// Reverb & Effects
			ReverbZoneMix = 1f;
			BypassEffects = false;
			BypassListenerEffects = false;
			BypassReverbZones = false;

			// Listener Ignore
			IgnoreListenerVolume = false;
			IgnoreListenerPause = false;

			// Velocity & Output
			VelocityUpdateMode = AudioVelocityUpdateMode.Auto;
			OutputAudioMixerGroup = null;

			// Playback (resets on clip reset)
			//Time = 0f;
		}

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
