using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// MonoBehaviour that keeps track of whichever states the <see cref="Animator"/> has entered.
	/// See: <seealso cref="NotifyAnimatorStateChange"/>.
	/// </summary>
	[RequireComponent(typeof(AnimatorWrapper))]
	public class AnimatorStateObserver : MonoBehaviour, IDependency
	{
		public event Action<string> AnimatorStateEnteredEvent;
		public event Action<string> AnimatorStateExitedEvent;

		[SerializeField, Tooltip("Will attempt to set bool parameters matching the currently entered states to true.")] private bool passStatesToParameters;

		private List<string> states = new List<string>();
		private List<string> heartBeats = new List<string>();
		private List<string> enteredStates = new List<string>();
		private List<string> exitedStates = new List<string>();

		private AnimatorWrapper animatorWrapper;

		protected void Awake()
		{
			animatorWrapper = GetComponent<AnimatorWrapper>();
		}

		public void SignalHeartbeat(string characterState)
		{
			if (!heartBeats.Contains(characterState))
			{
				heartBeats.Add(characterState);
			}
		}

		public bool IsInState(string characterState)
		{
			return states.Contains(characterState);
		}

		public bool IsInAllStates(params string[] states)
		{
			bool allStates = true;

			foreach (var item in states)
			{
				allStates = IsInState(item);
			}

			return allStates;
		}

		public bool IsInAnyOfStates(params string[] states)
		{
			foreach (var item in states)
			{
				if (IsInState(item))
				{
					return true;
				}
			}

			return false;
		}

		protected void LateUpdate()
		{
			enteredStates.Clear();
			for (int i = 0; i < heartBeats.Count; i++)
			{
				if (!states.Contains(heartBeats[i]))
				{
					enteredStates.Add(heartBeats[i]);
					states.Add(heartBeats[i]);

					if (passStatesToParameters)
					{
						animatorWrapper.TrySetBool(heartBeats[i], true);
					}
				}
			}

			exitedStates.Clear();
			for (int i = states.Count - 1; i >= 0; i--)
			{
				if (!heartBeats.Contains(states[i]))
				{
					if (passStatesToParameters)
					{
						animatorWrapper.TrySetBool(states[i], false);
					}

					exitedStates.Add(states[i]);
					states.RemoveAt(i);
				}
			}

			heartBeats.Clear();

			foreach (string enteredState in enteredStates)
			{
				AnimatorStateEnteredEvent?.Invoke(enteredState);
			}
			foreach (string exitedState in exitedStates)
			{
				AnimatorStateExitedEvent?.Invoke(exitedState);
			}
		}
	}
}
