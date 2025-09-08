using SpaxUtils.UI;
using UnityEngine;

namespace SpaxUtils
{
	public class MenuScreenEnvironmentAudio : MonoBehaviour
	{
		[SerializeField] private EnvironmentAudioSettingsAsset settings;

		private UIScreenManager screenManager;
		private EnvironmentAudioManager environmentAudioManager;

		public void InjectDependencies(UIScreenManager screenManager, EnvironmentAudioManager environmentAudioManager)
		{
			this.screenManager = screenManager;
			this.environmentAudioManager = environmentAudioManager;
		}

		protected void OnEnable()
		{
			screenManager.ContextChangedEvent += OnContextChanged;
		}

		protected void OnDisable()
		{
			screenManager.ContextChangedEvent -= OnContextChanged;
		}

		private void OnContextChanged(string context)
		{
			if (!context.IsNullOrEmpty())
			{
				// In a menu or paused in one way or another.
				environmentAudioManager.Override(settings);
			}
			else
			{
				// Unpaused.
				environmentAudioManager.Override(null);
			}
		}
	}
}
