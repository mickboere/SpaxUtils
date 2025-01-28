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
				if (!_reverbFilter) { _reverbFilter = Listener.GetComponent<AudioReverbFilter>(); }
				return _reverbFilter;
			}
		}
		private AudioReverbFilter _reverbFilter;

		[SerializeField] private AudioListener listener;
		[SerializeField] private AudioMixer mixer;

		private RuntimeDataService runtimeDataService;

		public void InjectDependencies(RuntimeDataService runtimeDataService)
		{
			this.runtimeDataService = runtimeDataService;

			InitializeListener();
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
			if (!_listener)
			{
				_listener = Instantiate(listener);
				DontDestroyOnLoad(_listener);
			}
		}
	}
}
