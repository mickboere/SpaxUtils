using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Callback service that supports different priority levels of update loops that get smeared out over time.
	/// </summary>
	public class OptimizedCallbackService : IService
	{
		// TOP is called every frame without smearing.
		private const float HIGH_INTERVAL = 0.2f; // HIGH callback window in seconds.
		private const float MEDIUM_INTERVAL = 0.5f; // MEDIUM callback window in seconds.
		private const float LOW_INTERVAL = 1.1f; // LOW callback window in seconds.
												 // CULLED is never invoked at all.
												 // Intervals are non-divisible to make stacked rythmic callbacks more unlikely.

		private CallbackService callbackService;
		private Dictionary<object, (int prio, Action<float> callback)> subscribers;
		private List<object> topPrio;
		private List<object> culledPrio;
		private Dictionary<int, FrameSmearer> smearers;

		public OptimizedCallbackService(CallbackService callbackService)
		{
			this.callbackService = callbackService;
			callbackService.UpdateCallback += OnUpdate;

			subscribers = new Dictionary<object, (int prio, Action<float> callback)>();
			topPrio = new List<object>();
			culledPrio = new List<object>();

			smearers = new Dictionary<int, FrameSmearer>();
			smearers.Add((int)PriorityLevel.High, new FrameSmearer(callbackService, HIGH_INTERVAL));
			smearers.Add((int)PriorityLevel.Medium, new FrameSmearer(callbackService, MEDIUM_INTERVAL));
			smearers.Add((int)PriorityLevel.Low, new FrameSmearer(callbackService, LOW_INTERVAL));
		}

		#region Subscribing

		/// <summary>
		/// Add a new subscriber that gets invoked every <paramref name="interval"/> milliseconds.
		/// 0-4 are reserved for the default <see cref="PriorityLevel"/>s.
		/// </summary>
		/// <param name="subscriber">The subscriber to add.</param>
		/// <param name="interval">If greater than 4; the max interval between callbacks in milliseconds, else; <see cref="PriorityLevel"/>.</param>
		/// <param name="callback">The callback to be invoked with the delta time between its last update in seconds.</param>
		public void Subscribe(object subscriber, int interval, Action<float> callback)
		{
			if (subscribers.ContainsKey(subscriber))
			{
				Unsubscribe(subscriber);
			}

			subscribers.Add(subscriber, (interval, callback));
			Add(subscriber, interval, callback);
		}

		public void Subscribe(object subscriber, PriorityLevel prio, Action<float> callback)
		{
			Subscribe(subscriber, (int)prio, callback);
		}

		public void Subscribe(object subscriber, float interval, Action<float> callback)
		{
			Subscribe(subscriber, interval.ToMilliseconds(), callback);
		}

		public void Unsubscribe(object subscriber)
		{
			if (subscribers.ContainsKey(subscriber))
			{
				int prio = subscribers[subscriber].prio;
				subscribers.Remove(subscriber);
				Remove(subscriber, prio);
			}
		}

		public void Switch(object subscriber, int interval)
		{
			if (subscribers.ContainsKey(subscriber))
			{
				(int prio, Action<float> callback) current = subscribers[subscriber];
				if (interval != current.prio)
				{
					Remove(subscriber, current.prio);
					subscribers[subscriber] = (interval, current.callback);
					Add(subscriber, interval, current.callback);
				}
			}
			else
			{
				SpaxDebug.Error("Couldn't switch subscriber priority level because it isn't subscribed yet.");
			}
		}

		public void Switch(object subscriber, PriorityLevel prio)
		{
			Switch(subscriber, (int)prio);
		}

		public void Switch(object subscriber, float interval)
		{
			Switch(subscriber, interval.ToMilliseconds());
		}

		private void Add(object subscriber, int prio, Action<float> callback)
		{
			switch (prio)
			{
				case (int)PriorityLevel.Top:
					topPrio.Add(subscriber);
					break;
				case (int)PriorityLevel.Culled:
					culledPrio.Add(subscriber);
					break;
				default:
					if (!smearers.ContainsKey(prio))
					{
						smearers.Add(prio, new FrameSmearer(callbackService, 0.001f * prio));
					}
					smearers[prio].Add(subscriber, callback);
					break;
			}
		}

		private void Remove(object subscriber, int prio)
		{
			switch (prio)
			{
				case (int)PriorityLevel.Top:
					topPrio.Remove(subscriber);
					break;
				case (int)PriorityLevel.Culled:
					culledPrio.Remove(subscriber);
					break;
				default:
					smearers[prio].Remove(subscriber);
					if (prio > 4 && smearers[prio].Count == 0)
					{
						smearers[prio].Dispose();
						smearers.Remove(prio);
					}
					break;
			}
		}

		#endregion Subscribing

		private void OnUpdate()
		{
			// Only TOP priority subscribers are invoked every single frame.
			foreach (object subscriber in topPrio)
			{
				subscribers[subscriber].callback(Time.deltaTime);
			}
		}
	}
}
