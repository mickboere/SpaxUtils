using System;
using System.Collections.Generic;
using SpaxUtils.UI;
using UnityEngine;

namespace SpaxUtils
{
	public class MenuScreenEnvironmentAudio : MonoBehaviour
	{
		[Serializable]
		private struct ContextAudioEntry
		{
			[ConstDropdown(typeof(IContextIdentifiers))] public string Context;
			public EnvironmentAudioSettingsAsset Settings;
		}

		[SerializeField] private List<ContextAudioEntry> contextAudioEntries = new List<ContextAudioEntry>();

		private readonly object overrideKey = new object();

		private UIScreenManager screenManager;
		private EnvironmentAudioManager environmentAudioManager;

		private bool overrideActive;
		private IEnvironmentAudioSettings currentOverrideSettings;

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

			if (overrideActive)
			{
				environmentAudioManager.PopOverride(overrideKey);
				overrideActive = false;
				currentOverrideSettings = null;
			}
		}

		private void OnContextChanged(string context)
		{
			IEnvironmentAudioSettings resolvedSettings = ResolveSettings(context);

			if (resolvedSettings == null)
			{
				if (overrideActive)
				{
					environmentAudioManager.PopOverride(overrideKey);
					overrideActive = false;
					currentOverrideSettings = null;
				}

				return;
			}

			if (overrideActive && ReferenceEquals(currentOverrideSettings, resolvedSettings))
			{
				// Same effective settings as previous context, keep current override alive.
				return;
			}

			if (overrideActive)
			{
				environmentAudioManager.PopOverride(overrideKey);
				overrideActive = false;
				currentOverrideSettings = null;
			}

			environmentAudioManager.PushOverride(overrideKey, resolvedSettings);
			overrideActive = true;
			currentOverrideSettings = resolvedSettings;
		}

		private IEnvironmentAudioSettings ResolveSettings(string context)
		{
			if (string.IsNullOrEmpty(context))
			{
				return null;
			}

			for (int i = 0; i < contextAudioEntries.Count; i++)
			{
				if (contextAudioEntries[i].Context == context)
				{
					return contextAudioEntries[i].Settings;
				}
			}

			return null;
		}
	}
}
