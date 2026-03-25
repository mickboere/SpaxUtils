using SpaxUtils;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(UIAudioManager), menuName = "ScriptableObjects/Audio/" + nameof(UIAudioManager))]
	public class UIAudioManager : ScriptableObject, IService
	{
		[SerializeField] private AudioSourceWrapper audioSourceWrapperPrefab;

		[Header("Navigation SFX")]
		[SerializeField] private SFXData navigationSFX;
		[SerializeField] private SFXData selectionSFX;
		[SerializeField] private SFXData confirmationSFX;
		[SerializeField] private SFXData cancelSFX;

		[Header("Playback")]
		[SerializeField, Min(0f)] private float cooldown = 0.05f;

		private AudioSourceWrapper audioSourceWrapperInstance;
		private bool isDisposed;
		private float lastPlayTime = float.NegativeInfinity;

		public void InjectDependencies()
		{
			if (audioSourceWrapperInstance != null)
			{
				return;
			}

			audioSourceWrapperInstance = CreateSource(audioSourceWrapperPrefab);
		}

		protected void OnDestroy()
		{
			DisposeService();
		}

		public void Play(SFXData sfx, float volume = 1f)
		{
			if (isDisposed || !CanPlay())
			{
				return;
			}

			sfx?.PlayOneShot(audioSourceWrapperInstance, volume);
		}

		public void PlayNavigation()
		{
			Play(navigationSFX);
		}

		public void PlaySelection()
		{
			Play(selectionSFX);
		}

		public void PlayConfirmation()
		{
			Play(confirmationSFX);
		}

		public void PlayCancel()
		{
			Play(cancelSFX);
		}

		private bool CanPlay()
		{
			float time = Time.unscaledTime;
			if (time - lastPlayTime < cooldown)
			{
				return false;
			}

			lastPlayTime = time;
			return true;
		}

		private AudioSourceWrapper CreateSource(AudioSourceWrapper prefab)
		{
			if (prefab == null)
			{
				Debug.LogError($"{nameof(UIAudioManager)} requires an {nameof(AudioSourceWrapper)} prefab.");
				return null;
			}

			AudioSourceWrapper instance = Instantiate(prefab);
			if (instance == null)
			{
				return null;
			}

			AudioSource source = instance.AudioSource;
			source.playOnAwake = false;
			source.loop = false;
			source.spatialBlend = 0f;

			DontDestroyOnLoad(instance.gameObject);
			return instance;
		}

		private void DisposeService()
		{
			if (isDisposed)
			{
				return;
			}

			isDisposed = true;

			if (audioSourceWrapperInstance != null)
			{
				Destroy(audioSourceWrapperInstance.gameObject);
				audioSourceWrapperInstance = null;
			}
		}
	}
}
