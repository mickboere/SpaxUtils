using SpaxUtils;
using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Class that handles counting clicks per second.
	/// </summary>
	public class ClicksPerSecondHelper : IDisposable
	{
		public float ClicksPerSecond => clicks.Count / clickBuffer;

		private float Time => unscaledTime ? UnityEngine.Time.unscaledTime : UnityEngine.Time.time;

		private CallbackService callbackService;
		private float clickBuffer;
		private bool unscaledTime;
		private List<float> clicks;

		public ClicksPerSecondHelper(CallbackService callbackService, float clickBuffer = 1f, bool unscaledTime = true)
		{
			this.callbackService = callbackService;
			this.clickBuffer = clickBuffer;
			this.unscaledTime = unscaledTime;
			clicks = new List<float>();

			callbackService.UpdateCallback += OnUpdate;
		}

		public void Dispose()
		{
			callbackService.UpdateCallback -= OnUpdate;
		}

		public void Click()
		{
			clicks.Add(Time);
		}

		private void OnUpdate()
		{
			// Remove all clicks that exceed the buffer time.
			for (int i = 0; i < clicks.Count; i++)
			{
				if (Time > clicks[i] + clickBuffer)
				{
					clicks.RemoveAt(i);
					i--;
				}
			}
		}
	}
}
