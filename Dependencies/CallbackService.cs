﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Service that gives access to all types of update callbacks, including those of a <see cref="MonoBehaviour"/> like Update, FixedUpdate, LateUpdate, but also has support for callbacks with custom intervals.
	/// </summary>
	[DefaultExecutionOrder(-1)]
	public class CallbackService : MonoBehaviour, IServiceComponent
	{
		/// <summary>
		/// Unordered FixedUpdate callback.
		/// </summary>
		public event Action FixedUpdateCallback;

		/// <summary>
		/// Unordered Update callback.
		/// </summary>
		public event Action UpdateCallback;

		/// <summary>
		/// Unordered LateUpdate callback.
		/// </summary>
		public event Action LateUpdateCallback;

		/// <summary>
		/// OnDrawGizmos() callback.
		/// </summary>
		public event Action DrawGizmosCallback;

		private Dictionary<int, Dictionary<object, Action<float>>> loops = new Dictionary<int, Dictionary<object, Action<float>>>();
		private Dictionary<int, Coroutine> coroutines = new Dictionary<int, Coroutine>();

		private List<(Action<float> callback, int order)> orderedFixedUpdateCallbacks = new List<(Action<float> callback, int order)>();
		private Dictionary<object, int> orderedFixedUpdateIndices = new Dictionary<object, int>();
		private List<(Action<float> callback, int order)> orderedUpdateCallbacks = new List<(Action<float> callback, int order)>();
		private Dictionary<object, int> orderedUpdateIndices = new Dictionary<object, int>();
		private List<(Action<float> callback, int order)> orderedLateUpdateCallbacks = new List<(Action<float> callback, int order)>();
		private Dictionary<object, int> orderedLateUpdateIndices = new Dictionary<object, int>();

		private bool unsubscribed;

		protected void FixedUpdate()
		{
			unsubscribed = false;
			for (int i = 0; i < orderedFixedUpdateCallbacks.Count; i++)
			{
				orderedFixedUpdateCallbacks[i].callback(Time.fixedDeltaTime);
				if (unsubscribed)
				{
					i--;
					unsubscribed = false;
				}
			}
			FixedUpdateCallback?.Invoke();
		}

		protected void Update()
		{
			unsubscribed = false;
			for (int i = 0; i < orderedUpdateCallbacks.Count; i++)
			{
				orderedUpdateCallbacks[i].callback(Time.deltaTime);
				if (unsubscribed)
				{
					i--;
					unsubscribed = false;
				}
			}
			UpdateCallback?.Invoke();
		}

		protected void LateUpdate()
		{
			unsubscribed = false;
			for (int i = 0; i < orderedLateUpdateCallbacks.Count; i++)
			{
				orderedLateUpdateCallbacks[i].callback(Time.deltaTime);
				if (unsubscribed)
				{
					i--;
					unsubscribed = false;
				}
			}
			LateUpdateCallback?.Invoke();
		}

		protected void OnDrawGizmos()
		{
			DrawGizmosCallback?.Invoke();
		}

		#region Ordered Callbacks

		/// <summary>
		/// Subscribes to updates of <paramref name="updateMode"/>.
		/// </summary>
		/// <param name="updateMode">The unity update mode to subscribe to. Do NOT use <see cref="UpdateMode.Custom"/> here!</param>
		/// <param name="subscriber">The subscriber object used to identify the subscription when unsubscribing.</param>
		/// <param name="callback">The method to invoke when the update loop is running.</param>
		/// <param name="order">The subscribe order, highest order is invoked last.</param>
		public void SubscribeUpdate(UpdateMode updateMode, object subscriber, Action<float> callback, int order = 0)
		{
			switch (updateMode)
			{
				case UpdateMode.FixedUpdate:
					Add(orderedFixedUpdateCallbacks, orderedFixedUpdateIndices, subscriber, callback, order);
					break;
				case UpdateMode.Update:
					Add(orderedUpdateCallbacks, orderedUpdateIndices, subscriber, callback, order);
					break;
				case UpdateMode.LateUpdate:
					Add(orderedLateUpdateCallbacks, orderedLateUpdateIndices, subscriber, callback, order);
					break;
				default:
					SpaxDebug.Error($"Update mode <{updateMode}> is not supperted with default subscribe method.", "Callback was not subscribed.");
					break;
			}

			void Add(List<(Action<float> callback, int order)> list, Dictionary<object, int> dict, object subscriber, Action<float> callback, int order)
			{
				// If empty just add.
				if (list.Count == 0)
				{
					dict[subscriber] = 0;
					list.Add((callback, order));
					return;
				}

				// Prevent duplicates.
				if (dict.ContainsKey(subscriber))
				{
					SpaxDebug.Error($"Duplicate subscription!", $"Subscriber of type {subscriber.GetType().FullName} is already subscribed.");
					return;
				}

				// If highest, add as last.
				if (order >= list[list.Count - 1].order)
				{
					dict[subscriber] = list.Count;
					list.Add((callback, order));
					return;
				}

				// Else insert in correct place.
				for (int i = 0; i < list.Count; i++)
				{
					if (order < list[i].order)
					{
						list.Insert(i, (callback, order));

						// Shift dictionary entries by 1.
						object[] keys = dict.Keys.ToArray();
						foreach (object key in keys)
						{
							int val = dict[key];
							if (val >= i)
							{
								dict[key] = val + 1;
							}
						}
						dict[subscriber] = i;

						return;
					}
				}

				SpaxDebug.Error($"Ordered Update", $"Subscriber could not be added for some reason. sub={subscriber}, order={order}");
			}
		}

		public void UnsubscribeUpdate(UpdateMode updateMode, object subscriber)
		{
			switch (updateMode)
			{
				case UpdateMode.FixedUpdate:
					RemoveSubscriber(orderedFixedUpdateCallbacks, orderedFixedUpdateIndices, subscriber);
					break;
				case UpdateMode.Update:
					RemoveSubscriber(orderedUpdateCallbacks, orderedUpdateIndices, subscriber);
					break;
				case UpdateMode.LateUpdate:
					RemoveSubscriber(orderedLateUpdateCallbacks, orderedLateUpdateIndices, subscriber);
					break;
			}
		}

		public void UnsubscribeUpdates(object subscriber)
		{
			RemoveSubscriber(orderedFixedUpdateCallbacks, orderedFixedUpdateIndices, subscriber);
			RemoveSubscriber(orderedUpdateCallbacks, orderedUpdateIndices, subscriber);
			RemoveSubscriber(orderedLateUpdateCallbacks, orderedLateUpdateIndices, subscriber);
		}

		private void RemoveSubscriber(List<(Action<float> callback, int order)> list, Dictionary<object, int> dict, object subscriber)
		{
			if (dict.ContainsKey(subscriber))
			{
				int index = dict[subscriber];
				list.RemoveAt(index);
				dict.Remove(subscriber);

				// Shove dictionary entries back by 1.
				object[] keys = dict.Keys.ToArray();
				foreach (object key in keys)
				{
					int val = dict[key];
					if (val > index)
					{
						dict[key] = val - 1;
					}
				}

				unsubscribed = true;
			}
		}

		#endregion Ordered Callbacks

		#region Custom Update Loops

		/// <summary>
		/// Adds a custom callback loop invoked at an interval of <paramref name="seconds"/>.
		/// </summary>
		/// <param name="listener">The listener object, used to unsubscribe from the callback.</param>
		/// <param name="seconds">The interval between callbacks in seconds.</param>
		/// <param name="callback">The method to invoke every callback. The float parameter is the actual delta time between invocations.</param>
		public void AddCustom(object listener, float seconds, Action<float> callback)
		{
			AddCustom(listener, ToMilliseconds(seconds), callback);
		}

		/// <summary>
		/// Adds a custom callback loop invoked at an interval of <paramref name="milliseconds"/>.
		/// </summary>
		/// <param name="listener">The listener object, used to unsubscribe from the callback.</param>
		/// <param name="milliseconds">The interval between callbacks in milliseconds.</param>
		/// <param name="callback">The method to invoke every callback. The float parameter is the actual delta time between invocations.</param>
		public void AddCustom(object listener, int milliseconds, Action<float> callback)
		{
			if (!loops.ContainsKey(milliseconds))
			{
				loops.Add(milliseconds, new Dictionary<object, Action<float>>());
			}

			loops[milliseconds][listener] = callback;

			if (!coroutines.ContainsKey(milliseconds))
			{
				coroutines.Add(milliseconds, StartCoroutine(StartCustomUpdateLoop(milliseconds)));
			}
		}

		/// <summary>
		/// Remove a listener from a custom callback loop.
		/// </summary>
		/// <param name="listener">The listener to remove.</param>
		/// <param name="seconds">The subscribed callback interval.</param>
		public void RemoveCustom(object listener, float seconds)
		{
			RemoveCustom(listener, ToMilliseconds(seconds));
		}

		/// <summary>
		/// Remove a listener from a custom callback loop.
		/// </summary>
		/// <param name="listener">The listener to remove.</param>
		/// <param name="milliseconds">The subscribed callback interval.</param>
		public void RemoveCustom(object listener, int milliseconds)
		{
			if (loops.ContainsKey(milliseconds))
			{
				if (loops[milliseconds].ContainsKey(listener))
				{
					loops[milliseconds].Remove(listener);
				}
				if (loops[milliseconds].Count == 0)
				{
					StopCoroutine(coroutines[milliseconds]);
					coroutines.Remove(milliseconds);
				}
			}
		}

		/// <summary>
		/// Removes <paramref name="listener"/> from all custom callback loops.
		/// </summary>
		/// <param name="listener"></param>
		public void RemoveCustom(object listener)
		{
			foreach (KeyValuePair<int, Dictionary<object, Action<float>>> loop in loops)
			{
				if (loop.Value.ContainsKey(listener))
				{
					RemoveCustom(listener, loop.Key);
				}
			}
		}

		private IEnumerator StartCustomUpdateLoop(int milliseconds)
		{
			float time = milliseconds / 1000f;
			float last = Time.unscaledTime;
			float timer = 0f;
			while (true)
			{
				yield return null;
				while (timer > time)
				{
					timer -= time;
					float delta = Time.unscaledTime - last;
					last = Time.unscaledTime;
					foreach (KeyValuePair<object, Action<float>> callback in loops[milliseconds])
					{
						callback.Value.Invoke(delta);
					}
				}
				timer += Time.unscaledDeltaTime;
			}
		}

		/// <summary>
		/// We use milliseconds to store each custom update loop as it's more accurate than floats and we never want to go lower than milliseconds anyways.
		/// </summary>
		private int ToMilliseconds(float seconds)
		{
			return Mathf.RoundToInt(seconds * 1000f);
		}

		#endregion
	}
}
