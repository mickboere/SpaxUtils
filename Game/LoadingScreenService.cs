using System;
using UnityEngine;
using SpaxUtils.UI;

namespace SpaxUtils
{
	/// <summary>
	/// Service that owns a persistent loading screen UI instance and drives its existing UIGroup transition.
	/// This is intentionally thin: it does NOT implement its own fade logic; it only forwards to UIGroup.
	/// </summary>
	public class LoadingScreenService : IService
	{
		/// <summary>
		/// Whether a loading screen instance exists and is currently active in the hierarchy.
		/// </summary>
		public bool Active => instance != null && instance.gameObject.activeInHierarchy;

		/// <summary>
		/// The instantiated loading screen root (DontDestroyOnLoad).
		/// </summary>
		public UIRoot Instance => instance;

		private readonly IDependencyManager dependencyManager;
		private readonly GameData gameData;

		private UIRoot instance;

		// Monotonic request id so stale callbacks (from older show/hide calls) don't fire after a newer request.
		private int requestVersion;

		/// <summary>
		/// Create the service with a loading screen prefab.
		/// The prefab is expected to have a UIRoot with a UIGroup that controls visibility via its Transition.
		/// </summary>
		public LoadingScreenService(IDependencyManager dependencyManager, GameData gameData)
		{
			this.dependencyManager = dependencyManager;
			this.gameData = gameData;

			EnsureInstance();
			HideImmediate();
		}

		/// <summary>
		/// Fade/show the loading screen using the UIGroup transition system.
		/// </summary>
		public void Show(Action callback = null, float duration = -1f)
		{
			EnsureInstance();

			// Supersede any prior request so only the latest callback fires.
			int version = ++requestVersion;

			instance.gameObject.SetActive(true);

			// Forward to your transition system.
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
		/// Immediately show the loading screen (no transition).
		/// </summary>
		public void ShowImmediate()
		{
			EnsureInstance();
			++requestVersion;

			instance.gameObject.SetActive(true);
			instance.UIGroup.ShowImmediately();
		}

		/// <summary>
		/// Fade/hide the loading screen using the UIGroup transition system.
		/// </summary>
		public void Hide(Action callback = null, float duration = -1f)
		{
			EnsureInstance();

			int version = ++requestVersion;

			// Forward to your transition system.
			instance.UIGroup.Hide(() =>
			{
				if (version != requestVersion)
				{
					return;
				}

				// Only deactivate after the transition has finished (so it can animate).
				if (instance != null)
				{
					instance.gameObject.SetActive(false);
				}

				callback?.Invoke();
			}, 0f, duration);
		}

		/// <summary>
		/// Immediately hide the loading screen (no transition) and deactivate it.
		/// </summary>
		public void HideImmediate()
		{
			EnsureInstance();
			++requestVersion;

			instance.UIGroup.HideImmediately();
			instance.gameObject.SetActive(false);
		}

		/// <summary>
		/// Ensures the loading UI exists. Safe to call repeatedly.
		/// </summary>
		private void EnsureInstance()
		{
			if (instance != null)
			{
				return;
			}

			if (gameData.LoadingScreenPrefab == null)
			{
				SpaxDebug.Error("No LoadingScreenPrefab prefab defined in GameData.", "Provide a UIRoot loading screen prefab.");
				return;
			}

			instance = DependencyUtils.InstantiateAndInject(gameData.LoadingScreenPrefab.gameObject, dependencyManager, true, false).GetComponent<UIRoot>();
			UnityEngine.Object.DontDestroyOnLoad(instance);
		}
	}
}
