using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpaxUtils
{
	/// <summary>
	/// Service that manages scene loading.
	/// </summary>
	public class SceneService : IService
	{
		public string CurrentScene => SceneManager.GetActiveScene().name;
		public bool IsLoading => coroutine != null;
		public string CurrentlyLoading { get; private set; }

		private CallbackService callbackService;

		private Coroutine coroutine;

		// Monotonic request id to prevent stale loads/callbacks from winning races.
		private int loadVersion;

		public SceneService(CallbackService callbackService)
		{
			this.callbackService = callbackService;
		}

		public void LoadScene(string scene, Action callback = null)
		{
			if (string.IsNullOrEmpty(scene))
			{
				SpaxDebug.Error("Can't load scene", "Scene name is null or empty.");
				return;
			}

			// Supersede any prior request (note: does not cancel Unity's async load, it only invalidates callbacks).
			loadVersion++;

			if (IsLoading)
			{
				callbackService.StopCoroutine(coroutine);
				coroutine = null;
			}

			CurrentlyLoading = scene;

			int version = loadVersion;
			string requestedScene = scene;
			coroutine = callbackService.StartCoroutine(Load(version, requestedScene, callback));
		}

		private IEnumerator Load(int version, string scene, Action callback)
		{
			AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(scene);

			while (!asyncLoad.isDone)
			{
				// If a newer request was issued, abandon this one (do not invoke callback).
				if (version != loadVersion)
				{
					yield break;
				}

				yield return null;
			}

			// If we got superseded right as we finished, don't finalize or callback.
			if (version != loadVersion)
			{
				yield break;
			}

			CurrentlyLoading = null;
			coroutine = null;
			callback?.Invoke();
		}
	}
}
