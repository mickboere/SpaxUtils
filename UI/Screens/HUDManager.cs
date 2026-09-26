using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils.UI
{
	/// <summary>
	/// Shows one child <see cref="HUDMode"/> at a time within the HUD screen: the latest request, else the default.
	/// </summary>
	public class HUDManager : MonoBehaviour
	{
		/// <summary>
		/// Invoked with the new mode whenever the shown mode changes.
		/// </summary>
		public event Action<string> ModeChangedEvent;

		public string CurrentMode { get; private set; }

		[SerializeField, ConstDropdown(typeof(IContextIdentifiers))] private string defaultMode;

		private readonly List<(object owner, string mode)> requests = new List<(object owner, string mode)>();
		private HUDMode[] modes;

		protected void Awake()
		{
			Apply(true);
		}

		/// <summary>
		/// Shows <paramref name="mode"/> until <paramref name="owner"/> releases it or a later request takes over.
		/// </summary>
		public void Request(object owner, string mode)
		{
			requests.RemoveAll((r) => r.owner == owner);
			requests.Add((owner, mode));
			Apply(false);
		}

		/// <summary>
		/// Drops <paramref name="owner"/>'s request, falling back to the previous one or the default mode.
		/// </summary>
		public void Release(object owner)
		{
			if (requests.RemoveAll((r) => r.owner == owner) > 0)
			{
				Apply(false);
			}
		}

		private void Apply(bool force)
		{
			// Requests can arrive before Awake, from siblings enabled in the same frame.
			if (modes == null)
			{
				modes = GetComponentsInChildren<HUDMode>(true);
				force = true;
			}

			string target = requests.Count > 0 ? requests[requests.Count - 1].mode : defaultMode;
			if (!force && target == CurrentMode)
			{
				return;
			}

			CurrentMode = target;
			foreach (HUDMode mode in modes)
			{
				if (mode.Mode == target)
				{
					mode.ShowImmediately();
				}
				else
				{
					mode.HideImmediately();
				}
			}
			ModeChangedEvent?.Invoke(target);
		}
	}
}
