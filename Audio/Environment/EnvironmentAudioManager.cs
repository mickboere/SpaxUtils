using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(EnvironmentAudioManager), menuName = "ScriptableObjects/Audio/" + nameof(EnvironmentAudioManager))]
	public class EnvironmentAudioManager : ScriptableObject, IService
	{
		private sealed class OverrideLayer
		{
			public object Key;
			public IEnvironmentAudioSettings Settings;
		}

		[SerializeField] private AudioSource ambienceAudioSource;
		[SerializeField] private AudioSource musicAudioSource;

		private PlayerAgentService playerAgentService;
		private WorldRegionService worldService;
		private AudioManager audioManager;

		private IAgent player;
		private bool awaitingRegister;

		private AudioFader ambience;
		private AudioFader music;

		private IEnvironmentAudioSettings currentSettings;
		private readonly List<OverrideLayer> overrideStack = new List<OverrideLayer>(4);

		public void InjectDependencies(CallbackService callbackService, PlayerAgentService playerAgentService, WorldRegionService worldService, AudioManager audioManager)
		{
			this.playerAgentService = playerAgentService;
			this.worldService = worldService;
			this.audioManager = audioManager;

			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);

			ambience = new AudioFader(Source(ambienceAudioSource), callbackService);
			music = new AudioFader(Source(musicAudioSource), callbackService);
		}

		protected void OnDestroy()
		{
			ambience?.Dispose();
			music?.Dispose();
		}

		public void PushOverride(object key, IEnvironmentAudioSettings settings)
		{
			if (key == null)
			{
				Debug.LogError($"{nameof(EnvironmentAudioManager)}.{nameof(PushOverride)} called with null key.");
				return;
			}

			if (settings == null)
			{
				Debug.LogError($"{nameof(EnvironmentAudioManager)}.{nameof(PushOverride)} called with null settings.");
				return;
			}

			if (ContainsOverrideKey(key))
			{
				Debug.LogError($"{nameof(EnvironmentAudioManager)}.{nameof(PushOverride)} called with duplicate key.");
				return;
			}

			overrideStack.Add(new OverrideLayer
			{
				Key = key,
				Settings = settings
			});

			ambience.PushOverride(
				key,
				settings.Ambience,
				settings.AmbienceTransition,
				0f,
				true,
				settings.Ambience == null ? 0f : settings.Ambience.length * Random.value);

			music.PushOverride(
				key,
				settings.Music,
				settings.MusicTransition,
				settings.Delay,
				settings.Loop,
				settings.Music != null && settings.RandomStart ? settings.Music.length * Random.value : 0f);

			audioManager.SetReverb(settings.Reverb);
		}

		public bool PopOverride(object key)
		{
			if (key == null)
			{
				Debug.LogError($"{nameof(EnvironmentAudioManager)}.{nameof(PopOverride)} called with null key.");
				return false;
			}

			if (overrideStack.Count == 0)
			{
				Debug.LogError($"{nameof(EnvironmentAudioManager)}.{nameof(PopOverride)} called with empty override stack.");
				return false;
			}

			OverrideLayer top = overrideStack[overrideStack.Count - 1];
			if (!ReferenceEquals(top.Key, key))
			{
				Debug.LogError(
					$"{nameof(EnvironmentAudioManager)}.{nameof(PopOverride)} must pop the top override layer. " +
					$"Attempted to pop a non-top key.");
				return false;
			}

			overrideStack.RemoveAt(overrideStack.Count - 1);

			ambience.PopOverride(key, top.Settings.AmbienceTransition);
			music.PopOverride(key, top.Settings.MusicTransition);

			if (overrideStack.Count > 0)
			{
				audioManager.SetReverb(overrideStack[overrideStack.Count - 1].Settings.Reverb);
			}
			else
			{
				audioManager.SetReverb(currentSettings != null ? currentSettings.Reverb : AudioReverbPreset.Off);
			}

			return true;
		}

		public void ClearOverrides()
		{
			if (overrideStack.Count == 0)
			{
				return;
			}

			overrideStack.Clear();
			ambience.ClearOverrides();
			music.ClearOverrides();
			audioManager.SetReverb(currentSettings != null ? currentSettings.Reverb : AudioReverbPreset.Off);
		}

		private bool ContainsOverrideKey(object key)
		{
			for (int i = 0; i < overrideStack.Count; i++)
			{
				if (ReferenceEquals(overrideStack[i].Key, key))
				{
					return true;
				}
			}

			return false;
		}

		private void OnUpdate(float delta)
		{
			if (player == null)
			{
				if (playerAgentService.PlayerAgent != null)
				{
					OnPlayerRegistered(playerAgentService.PlayerAgent);
				}
				else if (!awaitingRegister)
				{
					playerAgentService.PlayerRegisteredEvent += OnPlayerRegistered;
					awaitingRegister = true;
				}
			}
		}

		private void OnPlayerRegistered(IAgent p)
		{
			player = p;
			worldService.Subscribe(p.Transform, OnRegionChange);
			playerAgentService.PlayerRegisteredEvent -= OnPlayerRegistered;
			playerAgentService.PlayerDeregisteredEvent += OnPlayerDeregistered;
			awaitingRegister = false;

			OnRegionChange(worldService.GetRegion(player.Transform));
		}

		private void OnPlayerDeregistered(IAgent p)
		{
			if (player == p)
			{
				player = null;
				worldService.Unsubscribe(p.Transform, OnRegionChange);
				playerAgentService.PlayerDeregisteredEvent -= OnPlayerDeregistered;
				OnRegionChange(null);
			}
		}

		private void OnRegionChange(IWorldRegion worldRegion)
		{
			if (worldRegion == null)
			{
				SetCurrent(null);
				return;
			}

			if (worldRegion is WorldRegion region &&
				region.TryGetComponent(out WorldRegionAudioComponent audioComponent))
			{
				SetCurrent(audioComponent.Settings);
			}
			else
			{
				SetCurrent(null);
			}
		}

		private void SetCurrent(IEnvironmentAudioSettings settings)
		{
			currentSettings = settings;

			bool hidden = overrideStack.Count > 0;

			if (settings == null)
			{
				ambience.SetBase(null, null, 0f, true, 0f, hidden);
				music.SetBase(null, null, 0f, true, 0f, hidden);

				if (!hidden)
				{
					audioManager.SetReverb(AudioReverbPreset.Off);
				}

				return;
			}

			ambience.SetBase(
				settings.Ambience,
				settings.AmbienceTransition,
				0f,
				true,
				settings.Ambience == null ? 0f : settings.Ambience.length * Random.value,
				hidden);

			music.SetBase(
				settings.Music,
				settings.MusicTransition,
				settings.Delay,
				settings.Loop,
				settings.Music != null && settings.RandomStart ? settings.Music.length * Random.value : 0f,
				hidden);

			if (!hidden)
			{
				audioManager.SetReverb(settings.Reverb);
			}
		}

		private AudioSource Source(AudioSource prefab)
		{
			AudioSource instance = Instantiate(prefab);
			DontDestroyOnLoad(instance);
			return instance;
		}
	}
}
