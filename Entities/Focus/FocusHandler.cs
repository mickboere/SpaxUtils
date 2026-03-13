using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Entity component that maintains a priority-based stack of focus providers.
	/// Any system can register a focus provider with a priority; the highest-priority
	/// non-null result becomes the current focus point each update.
	/// </summary>
	public class FocusHandler : EntityComponentMono
	{
		public const int PRIORITY_COMBAT = 40;
		public const int PRIORITY_INTERACTION = 30;
		public const int PRIORITY_SOFT_ENTITY = 20;
		public const int PRIORITY_CAMERA = 10;

		/// <summary>
		/// The current world-space focus point, or null if no provider has a valid target.
		/// </summary>
		public Vector3? CurrentFocusPoint { get; private set; }

		private struct FocusEntry
		{
			public int Priority;
			public Func<Vector3?> Provider;
		}

		private List<FocusEntry> entries = new List<FocusEntry>();
		private CallbackService callbackService;

		public void InjectDependencies(CallbackService callbackService)
		{
			this.callbackService = callbackService;
		}

		private void OnEnable()
		{
			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		private void OnDisable()
		{
			callbackService.UnsubscribeUpdate(UpdateMode.Update, this);
		}

		private void OnUpdate(float delta)
		{
			CurrentFocusPoint = null;

			// Entries are kept sorted descending on insert; first non-null result wins.
			for (int i = 0; i < entries.Count; i++)
			{
				Vector3? result = entries[i].Provider();
				if (result.HasValue)
				{
					CurrentFocusPoint = result;
					return;
				}
			}
		}

		/// <summary>
		/// Registers a focus provider at the given priority.
		/// Higher priority providers are evaluated first.
		/// The provider should return null when it has no valid focus.
		/// </summary>
		public void Register(int priority, Func<Vector3?> provider)
		{
			FocusEntry entry = new FocusEntry { Priority = priority, Provider = provider };

			for (int i = 0; i < entries.Count; i++)
			{
				if (priority > entries[i].Priority)
				{
					entries.Insert(i, entry);
					return;
				}
			}

			entries.Add(entry);
		}

		/// <summary>
		/// Unregisters a previously registered focus provider.
		/// </summary>
		public void Unregister(Func<Vector3?> provider)
		{
			for (int i = entries.Count - 1; i >= 0; i--)
			{
				if (entries[i].Provider == provider)
				{
					entries.RemoveAt(i);
					return;
				}
			}
		}
	}
}
