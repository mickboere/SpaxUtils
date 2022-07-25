using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Service that gives access to all types of update callbacks, including those of a <see cref="MonoBehaviour"/> like Update, FixedUpdate, LateUpdate, but also has support for callbacks with custom intervals.
	/// </summary>
	public class CallbackService : MonoBehaviour, IComponentService
	{
		public event Action FixedUpdateCallback;
		public event Action UpdateCallback;
		public event Action LateUpdateCallback;

		private Dictionary<int, Dictionary<object, Action>> loops = new Dictionary<int, Dictionary<object, Action>>();
		private Dictionary<int, Coroutine> coroutines = new Dictionary<int, Coroutine>();

		protected void FixedUpdate()
		{
			FixedUpdateCallback?.Invoke();
		}

		protected void Update()
		{
			UpdateCallback?.Invoke();
		}

		protected void LateUpdate()
		{
			LateUpdateCallback?.Invoke();
		}

		#region Custom Update Loops

		/// <summary>
		/// Adds a custom callback loop invoked at an interval of <paramref name="seconds"/>.
		/// </summary>
		/// <param name="listener">The listener object, used to unsubscribe from the callback.</param>
		/// <param name="seconds">The interval between callbacks in seconds.</param>
		/// <param name="callback">The method to invoke every callback.</param>
		public void AddCustom(object listener, float seconds, Action callback)
		{
			AddCustom(listener, ToMilliseconds(seconds), callback);
		}

		/// <summary>
		/// Adds a custom callback loop invoked at an interval of <paramref name="milliseconds"/>.
		/// </summary>
		/// <param name="listener">The listener object, used to unsubscribe from the callback.</param>
		/// <param name="milliseconds">The interval between callbacks in milliseconds.</param>
		/// <param name="callback">The method to invoke every callback.</param>
		public void AddCustom(object listener, int milliseconds, Action callback)
		{
			if (!loops.ContainsKey(milliseconds))
			{
				loops.Add(milliseconds, new Dictionary<object, Action>());
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
			foreach (KeyValuePair<int, Dictionary<object, Action>> loop in loops)
			{
				if (loop.Value.ContainsKey(listener))
				{
					RemoveCustom(listener, loop.Key);
				}
			}
		}

		private IEnumerator StartCustomUpdateLoop(int milliseconds)
		{
			float seconds = milliseconds / 1000f;
			float timer = 0f;
			while (true)
			{
				yield return null;
				if (timer > seconds)
				{
					timer -= seconds;
					foreach (KeyValuePair<object, Action> callback in loops[milliseconds])
					{
						callback.Value.Invoke();
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
