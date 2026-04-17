using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(AudioManager), menuName = "ScriptableObjects/Audio/" + nameof(AudioManager))]
	public class AudioManager : ScriptableObject, IService
	{
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

		// TODO: Save & load mixer settings.
		private RuntimeDataService runtimeDataService;

		private bool quitting;

		public void InjectDependencies(RuntimeDataService runtimeDataService)
		{
			this.runtimeDataService = runtimeDataService;

			// Track application quit explicitly (instead of frameCount hacks).
			// This prevents creating new audio objects during shutdown.
			Application.quitting -= OnApplicationQuitting;
			Application.quitting += OnApplicationQuitting;

			InitializeListener();
		}

		protected void OnDestroy()
		{
			Application.quitting -= OnApplicationQuitting;

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

		private void OnApplicationQuitting()
		{
			quitting = true;
		}
	}
}
