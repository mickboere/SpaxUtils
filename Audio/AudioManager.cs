using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Audio;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(AudioManager), menuName = "ScriptableObjects/Audio/" + nameof(AudioManager))]
	public class AudioManager : ScriptableObject, IService, ISettingsSupplier
	{
		private const string VOLUME = "Audio/Volume";
		private const float MIN_LINEAR = 0.0001f; // -80dB, the mixer floor.
		private const string VOLUME_SUFFIX = "Volume";

		public AudioListener Listener
		{
			get
			{
				if (!_listener) { InitializeListener(); }
				return _listener;
			}
		}
		private AudioListener _listener;

		public AudioReverbFilter ReverbFilter
		{
			get
			{
				if (!_reverbFilter && Listener) { _reverbFilter = Listener.GetComponent<AudioReverbFilter>(); }
				return _reverbFilter;
			}
		}
		private AudioReverbFilter _reverbFilter;

		[SerializeField] private AudioListener listener;
		[SerializeField] private AudioMixer mixer;

		[Header("Volume (0dB at 100%, perceptual curve)")]
		[Setting("audio.master", VOLUME, "Master", 0f, 1f, Format = "0%")]
		[SerializeField] private FloatSetting masterVolume = new FloatSetting(1f);
		[Setting("audio.sfx", VOLUME, "SFX", 0f, 1f, Format = "0%")]
		[SerializeField] private FloatSetting sfxVolume = new FloatSetting(1f);
		[Setting("audio.ambience", VOLUME, "Ambience", 0f, 1f, Format = "0%")]
		[SerializeField] private FloatSetting ambienceVolume = new FloatSetting(1f);
		[Setting("audio.music", VOLUME, "Music", 0f, 1f, Format = "0%")]
		[SerializeField] private FloatSetting musicVolume = new FloatSetting(1f);
		[Setting("audio.speech", VOLUME, "Speech", 0f, 1f, Format = "0%")]
		[SerializeField] private FloatSetting speechVolume = new FloatSetting(1f);
		[Setting("audio.ui", VOLUME, "UI", 0f, 1f, Format = "0%")]
		[SerializeField] private FloatSetting uiVolume = new FloatSetting(1f);

		private bool quitting;
		private (FloatSetting setting, string group)[] volumes;
		private readonly List<string> exposed = new List<string>();

		public void InjectDependencies()
		{
			// Track application quit explicitly (instead of frameCount hacks).
			// This prevents creating new audio objects during shutdown.
			Application.quitting -= OnApplicationQuitting;
			Application.quitting += OnApplicationQuitting;

			InitializeListener();
			InitializeVolumes();
		}

		protected void OnDestroy()
		{
			Application.quitting -= OnApplicationQuitting;

			// The mixer asset keeps exposed values across play sessions; hand them back to the snapshot.
			foreach (string parameter in exposed)
			{
				mixer.ClearFloat(parameter);
			}

			if (_listener)
			{
				Destroy(_listener.gameObject);
			}
		}

		public void ClaimListener(Transform parent)
		{
			Listener.transform.SetParent(parent);
			Listener.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
		}

		public void SetReverb(AudioReverbPreset preset)
		{
			if (ReverbFilter)
			{
				ReverbFilter.reverbPreset = preset;
			}
		}

		private void InitializeListener()
		{
			if (quitting || !Application.isPlaying)
			{
				return;
			}

			if (!_listener)
			{
				_listener = Instantiate(listener);
				DontDestroyOnLoad(_listener);
			}
		}

		/// <summary>
		/// Each volume setting with the mixer group it drives; the exposed parameter is the group name + "Volume".
		/// </summary>
		private (FloatSetting setting, string group)[] GetVolumes()
		{
			return new (FloatSetting, string)[]
			{
				(masterVolume, "Master"),
				(sfxVolume, "SFX"),
				(ambienceVolume, "Ambience"),
				(musicVolume, "Music"),
				(speechVolume, "Speech"),
				(uiVolume, "UI")
			};
		}

		private void InitializeVolumes()
		{
			volumes = GetVolumes();
			exposed.Clear();
			foreach ((FloatSetting setting, string group) in volumes)
			{
				if (mixer.GetFloat(group + VOLUME_SUFFIX, out _))
				{
					exposed.Add(group + VOLUME_SUFFIX);
					setting.ChangedEvent += OnVolumeChanged;
					ApplyVolume(setting, group);
				}
				else
				{
					SpaxDebug.Error("Mixer parameter is not exposed.", group + VOLUME_SUFFIX);
				}
			}
		}

		private void OnVolumeChanged(Setting setting, int player)
		{
			foreach ((FloatSetting volume, string group) in volumes)
			{
				if (volume == setting)
				{
					ApplyVolume(volume, group);
				}
			}
		}

		private void ApplyVolume(FloatSetting setting, string group)
		{
			mixer.SetFloat(group + VOLUME_SUFFIX, ToDecibels(setting.Value));
		}

		/// <summary>
		/// Squared slider → amplitude spreads perceived loudness evenly: 100% = 0dB, 50% ≈ -12dB.
		/// </summary>
		private static float ToDecibels(float slider)
		{
			float amplitude = slider * slider;
			return Mathf.Max(20f * Mathf.Log10(Mathf.Max(amplitude, MIN_LINEAR)), -80f);
		}

#if UNITY_EDITOR
		protected void OnValidate()
		{
			if (!Application.isPlaying && mixer != null)
			{
				// Deferred, since other assets can't be dirtied from within OnValidate.
				UnityEditor.EditorApplication.delayCall -= WriteVolumesToSnapshot;
				UnityEditor.EditorApplication.delayCall += WriteVolumesToSnapshot;
			}
		}

		/// <summary>
		/// Mirrors the default volumes into the mixer's start snapshot so the mixer window shows the same mix.
		/// Uses the mixer's internal editor API, as Unity exposes no public way to edit snapshots.
		/// </summary>
		private void WriteVolumesToSnapshot()
		{
			if (this == null || mixer == null || Application.isPlaying)
			{
				return;
			}

			const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			object snapshot = mixer.GetType().GetProperty("startSnapshot", FLAGS)?.GetValue(mixer);
			bool changed = false;
			foreach ((FloatSetting setting, string group) in GetVolumes())
			{
				AudioMixerGroup target = Array.Find(mixer.FindMatchingGroups(group), g => g.name == group);
				MethodInfo get = target?.GetType().GetMethod("GetValueForVolume", FLAGS);
				MethodInfo set = target?.GetType().GetMethod("SetValueForVolume", FLAGS);
				if (snapshot == null || get == null || set == null)
				{
					SpaxDebug.Error("Can't write volume to the mixer snapshot.",
						$"Group '{group}' or Unity's internal mixer API not found.");
					return;
				}

				float db = ToDecibels(Mathf.Clamp01(setting.Default));
				if (!Mathf.Approximately((float)get.Invoke(target, new object[] { mixer, snapshot }), db))
				{
					set.Invoke(target, new object[] { mixer, snapshot, db });
					changed = true;
				}
			}

			if (changed)
			{
				UnityEditor.EditorUtility.SetDirty(mixer);
			}
		}
#endif

		private void OnApplicationQuitting()
		{
			quitting = true;
		}
	}
}
