using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(EnvironmentAudioManager), menuName = "ScriptableObjects/Audio/" + nameof(EnvironmentAudioManager))]
	public class EnvironmentAudioManager : ScriptableObject, IService
	{
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
		private IEnvironmentAudioSettings overrideSettings;
		private float musicTimeBackup;

		public void InjectDependencies(CallbackService callbackService, PlayerAgentService playerAgentService, WorldRegionService worldService, AudioManager audioManager)
		{
			this.playerAgentService = playerAgentService;
			this.worldService = worldService;
			this.audioManager = audioManager;

			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);

			ambience = new AudioFader(Source(ambienceAudioSource), Source(ambienceAudioSource), callbackService);
			music = new AudioFader(Source(musicAudioSource), Source(musicAudioSource), callbackService);
		}

		protected void OnDestroy()
		{
			ambience?.Dispose();
			music?.Dispose();
		}

		public void Override(IEnvironmentAudioSettings settings)
		{
			if (settings == null && overrideSettings != null)
			{
				// Remove override.
				Switch(currentSettings, overrideSettings.AmbienceTransition, overrideSettings.MusicTransition, 0f, musicTimeBackup);
				overrideSettings = null;
			}
			else if (overrideSettings != settings)
			{
				// Apply override.
				overrideSettings = settings;
				musicTimeBackup = music.CurrentAudioSource.clip == null ? 0f : music.CurrentAudioSource.time;
				Switch(settings);
			}
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
		}

		private void SetCurrent(IEnvironmentAudioSettings settings)
		{
			currentSettings = settings;
			if (overrideSettings == null)
			{
				Switch(currentSettings);
			}
		}

		private void Switch(IEnvironmentAudioSettings settings,
			TransitionSettings ambienceTransitionOverride = null, TransitionSettings musicTransitionOverride = null,
			float delayOverride = -1f, float timeOverride = -1f)
		{
			if (settings == null)
			{
				ambience.Fade(null, null);
				music.Fade(null, null);
				audioManager.SetReverb(AudioReverbPreset.Off);
			}
			else
			{
				ambience.Fade(settings.Ambience,
					ambienceTransitionOverride ?? settings.AmbienceTransition,
					startTime: settings.Ambience == null ? 0f : settings.Ambience.length * Random.value);

				music.Fade(settings.Music,
					musicTransitionOverride ?? settings.MusicTransition,
					delayOverride < 0f ? settings.Delay : delayOverride,
					settings.Loop,
					timeOverride < 0f ? (settings.Music != null && settings.RandomStart ? settings.Music.length * Random.value : 0f) : timeOverride);

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
