using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpaxUtils
{
	public class SceneService : IService
	{
		public string CurrentScene => SceneManager.GetActiveScene().name;
		public bool IsLoading => coroutine != null;
		public string CurrentlyLoading { get; private set; }

		private CallbackService callbackService;

		private Coroutine coroutine;

		public SceneService(CallbackService callbackService)
		{
			this.callbackService = callbackService;
		}

		public void LoadScene(string scene, Action callback = null)
		{
			if (IsLoading)
			{
				callbackService.StopCoroutine(coroutine);
			}
			CurrentlyLoading = scene;
			coroutine = callbackService.StartCoroutine(Load(callback));
		}

		private IEnumerator Load(Action callback)
		{
			AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(CurrentlyLoading);
			while (!asyncLoad.isDone)
			{
				yield return null;
			}
			CurrentlyLoading = null;
			callback?.Invoke();
		}
	}
}
