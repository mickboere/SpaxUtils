using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Helper class for smearing out update loops over an interval.
	/// </summary>
	public class FrameSmearer : IDisposable
	{
		public int Count => subscribers.Count;

		private float CurrentTime => realtime ? Time.unscaledTime : Time.time;
		private float Delta => realtime ? Time.unscaledDeltaTime : Time.deltaTime;

		private CallbackService callbackService;
		private float maxInterval;
		private bool realtime;

		private List<object> subscribers;
		private Dictionary<object, Action<float>> callbacks;
		private Dictionary<object, float> updateTimes;

		private int index;
		private TimerStruct timer;

		public FrameSmearer(CallbackService callbackService, float maxInterval, bool realtime = false)
		{
			this.callbackService = callbackService;
			this.maxInterval = maxInterval;
			this.realtime = realtime;

			subscribers = new List<object>();
			callbacks = new Dictionary<object, Action<float>>();
			updateTimes = new Dictionary<object, float>();

			callbackService.UpdateCallback += OnUpdate;
		}

		public void Dispose()
		{
			callbackService.UpdateCallback -= OnUpdate;
			subscribers.Clear();
			callbacks.Clear();
			updateTimes.Clear();
		}

		public void Add(object subscriber, Action<float> callback)
		{
			if (subscribers.Contains(subscriber))
			{
				SpaxDebug.Error("Subscriber is already added to frame smearer.");
				return;
			}

			subscribers.Add(subscriber);
			callbacks.Add(subscriber, callback);
			updateTimes.Add(subscriber, CurrentTime);
		}

		public void Remove(object subscriber)
		{
			if (subscribers.Contains(subscriber))
			{
				if (index > subscribers.IndexOf(subscriber))
				{
					index--;
				}
				subscribers.Remove(subscriber);
				callbacks.Remove(subscriber);
				updateTimes.Remove(subscriber);
			}
		}

		private void OnUpdate()
		{
			if (subscribers.Count > 0 && !timer)
			{
				// How long should the interval be to fit all subscribers within the MaxInterval.
				float interval = maxInterval / callbacks.Count;

				// How many subscribers should be invoked at once to ensure they are called at least once every MaxInterval.
				// (how many times does the desired interval fit within delta)
				int stack = Mathf.CeilToInt(Delta / interval);

				// Invoke the subscribers.
				for (int i = 0; i < stack; i++)
				{
					Smear();
				}

				// Set new timer.
				timer = new TimerStruct(interval, realtime);
			}
		}

		private void Smear()
		{
			// Ensure valid index.
			if (index >= subscribers.Count)
			{
				index = 0;
			}
			object subscriber = subscribers[index];

			// Calculate delta since last update.
			float delta = CurrentTime - updateTimes[subscriber];
			updateTimes[subscriber] = CurrentTime;

			// Invoke callback and increase index for next loop.
			callbacks[subscriber](delta);
			index++;
		}
	}
}
