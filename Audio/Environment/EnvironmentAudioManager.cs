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

		private CallbackService callbackService;
		private PlayerAgentService playerAgentService;
		private WorldRegionService worldService;
		private AudioManager audioManager;

		private IAgent player;
		private bool awaitingRegister;
		private bool isDisposed;

		private AudioFader ambience;
		private AudioFader music;

		private IEnvironmentAudioSettings currentSettings;
		private readonly List<OverrideLayer> overrideStack = new List<OverrideLayer>(4);

		public void InjectDependencies(CallbackService callbackService, PlayerAgentService playerAgentService, WorldRegionService worldService, AudioManager audioManager)
		{
			this.callbackService = callbackService;
			this.playerAgentService = playerAgentService;
			this.worldService = worldService;
			this.audioManager = audioManager;

			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);

			ambience = new AudioFader(Source(ambienceAudioSource), callbackService);
			music = new AudioFader(Source(musicAudioSource), callbackService);
		}

		protected void OnDestroy()
		{
			DisposeManager();
		}

		public void PushOverride(object key, IEnvironmentAudioSettings settings)
		{
			if (isDisposed)
			{
				return;
			}

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

			int existingIndex = FindOverrideIndex(key);
			if (existingIndex >= 0)
			{
				if (existingIndex == overrideStack.Count - 1)
				{
					OverrideLayer existingTop = overrideStack[existingIndex];
					existingTop.Settings = settings;

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
					return;
				}

				overrideStack.RemoveAt(existingIndex);
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
			if (isDisposed)
			{
				return false;
			}

			if (key == null)
			{
				Debug.LogError($"{nameof(EnvironmentAudioManager)}.{nameof(PopOverride)} called with null key.");
				return false;
			}

			if (overrideStack.Count == 0)
			{
				return false;
			}

			OverrideLayer top = overrideStack[overrideStack.Count - 1];
			if (!ReferenceEquals(top.Key, key))
			{
				return false;
			}

			overrideStack.RemoveAt(overrideStack.Count - 1);

			ambience.PopOverride(key, top.Settings.AmbienceTransition);
			music.PopOverride(key, top.Settings.MusicTransition);

			if (overrideStack.Count > 0)
			{
				audioManager.SetReverb(overrideStack[overrideStack.Count - 1].Settings.Reverb);
			}
			else if (currentSettings != null)
			{
				audioManager.SetReverb(currentSettings.Reverb);
			}

			return true;
		}

		public void ClearOverrides()
		{
			if (isDisposed || overrideStack.Count == 0)
			{
				return;
			}

			overrideStack.Clear();
			ambience.ClearOverrides();
			music.ClearOverrides();

			if (currentSettings != null)
			{
				audioManager.SetReverb(currentSettings.Reverb);
			}
		}

		private int FindOverrideIndex(object key)
		{
			for (int i = 0; i < overrideStack.Count; i++)
			{
				if (ReferenceEquals(overrideStack[i].Key, key))
				{
					return i;
				}
			}

			return -1;
		}

		private void OnUpdate(float delta)
		{
			if (isDisposed)
			{
				return;
			}

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
			if (isDisposed || p == null)
			{
				return;
			}

			player = p;
			worldService.Subscribe(p.Transform, OnRegionChange);
			playerAgentService.PlayerRegisteredEvent -= OnPlayerRegistered;
			playerAgentService.PlayerDeregisteredEvent += OnPlayerDeregistered;
			awaitingRegister = false;

			OnRegionChange(worldService.GetRegion(player.Transform));
		}

		private void OnPlayerDeregistered(IAgent p)
		{
			if (isDisposed)
			{
				return;
			}

			if (player == p)
			{
				player = null;
				worldService.Unsubscribe(p.Transform, OnRegionChange);
				playerAgentService.PlayerDeregisteredEvent -= OnPlayerDeregistered;
			}
		}

		private void OnRegionChange(IWorldRegion worldRegion)
		{
			if (isDisposed)
			{
				return;
			}

			if (worldRegion is WorldRegion region &&
				region.TryGetComponent(out WorldRegionAudioComponent audioComponent) &&
				audioComponent.Settings != null)
			{
				SetCurrent(audioComponent.Settings);
			}
		}

		private void SetCurrent(IEnvironmentAudioSettings settings)
		{
			if (isDisposed || settings == null)
			{
				return;
			}

			currentSettings = settings;

			bool hidden = overrideStack.Count > 0;

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
			if (prefab == null)
			{
				return null;
			}

			AudioSource instance = Instantiate(prefab);
			if (instance == null)
			{
				return null;
			}

			DontDestroyOnLoad(instance.gameObject);
			return instance;
		}

		private void DisposeManager()
		{
			if (isDisposed)
			{
				return;
			}

			isDisposed = true;

			if (callbackService != null)
			{
				callbackService.UnsubscribeUpdates(this);
			}

			if (playerAgentService != null)
			{
				playerAgentService.PlayerRegisteredEvent -= OnPlayerRegistered;
				playerAgentService.PlayerDeregisteredEvent -= OnPlayerDeregistered;
			}

			if (player != null && worldService != null)
			{
				worldService.Unsubscribe(player.Transform, OnRegionChange);
			}

			awaitingRegister = false;
			player = null;
			overrideStack.Clear();

			ambience?.Dispose();
			ambience = null;

			music?.Dispose();
			music = null;
		}
	}
}
