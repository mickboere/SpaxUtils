using System;
using UnityEngine;
using SpaxUtils.UI;

namespace SpaxUtils
{
	/// <summary>
	/// Service that owns a persistent screen fade UI instance and drives its UIGroup transition.
	/// Used for cinematic fades to/from black without any loading indicators.
	/// </summary>
	public class ScreenFadeService : IService
	{
		/// <summary>
		/// Whether the fade screen is currently active in the hierarchy.
		/// </summary>
		public bool Active => instance != null && instance.gameObject.activeInHierarchy;

		/// <summary>
		/// The instantiated fade screen root (DontDestroyOnLoad).
		/// </summary>
		public UIRoot Instance => instance;

		private readonly IDependencyManager dependencyManager;
		private readonly GameData gameData;

		private UIRoot instance;

		// Monotonic request id so stale callbacks (from older show/hide calls) don't fire after a newer request.
		private int requestVersion;

		public ScreenFadeService(IDependencyManager dependencyManager, GameData gameData)
		{
			this.dependencyManager = dependencyManager;
			this.gameData = gameData;
			EnsureInstance();
			HideImmediate();
		}

		/// <summary>
		/// Fade to black using the UIGroup transition system.
		/// </summary>
		public void Show(Action callback = null, float duration = -1f)
		{
			EnsureInstance();

			int version = ++requestVersion;
			instance.gameObject.SetActive(true);

			instance.UIGroup.Show(() =>
			{
				if (version != requestVersion)
				{
					return;
				}
				callback?.Invoke();
			}, 0f, duration);
		}

		/// <summary>
		/// Immediately show the fade screen (no transition).
		/// </summary>
		public void ShowImmediate()
		{
			EnsureInstance();
			++requestVersion;
			instance.gameObject.SetActive(true);
			instance.UIGroup.ShowImmediately();
		}

		/// <summary>
		/// Fade from black using the UIGroup transition system.
		/// </summary>
		public void Hide(Action callback = null, float duration = -1f)
		{
			EnsureInstance();

			int version = ++requestVersion;

			instance.UIGroup.Hide(() =>
			{
				if (version != requestVersion)
				{
					return;
				}
				if (instance != null)
				{
					instance.gameObject.SetActive(false);
				}
				callback?.Invoke();
			}, 0f, duration);
		}

		/// <summary>
		/// Immediately hide the fade screen (no transition) and deactivate it.
		/// </summary>
		public void HideImmediate()
		{
			EnsureInstance();
			++requestVersion;
			instance.UIGroup.HideImmediately();
			instance.gameObject.SetActive(false);
		}

		/// <summary>
		/// Ensures the fade UI exists. Safe to call repeatedly.
		/// </summary>
		private void EnsureInstance()
		{
			if (instance != null)
			{
				return;
			}

			if (gameData.ScreenFadePrefab == null)
			{
				SpaxDebug.Error("No ScreenFadePrefab defined in GameData.", "Provide a UIRoot screen fade prefab.");
				return;
			}

			instance = DependencyUtils.InstantiateAndInject(gameData.ScreenFadePrefab.gameObject, dependencyManager, true, false).GetComponent<UIRoot>();
			UnityEngine.Object.DontDestroyOnLoad(instance);
		}
	}
}
