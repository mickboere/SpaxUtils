using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace SpaxUtils.UI
{
	/// <summary>
	/// Selection that respects split-screen: Selectable.Select() always goes through the first global event system,
	/// which hands one player's selection (and highlighter) to another player's UI.
	/// </summary>
	public static class SelectionUtils
	{
		/// <summary>
		/// Selects <paramref name="target"/> through the event system of the player whose UI contains it.
		/// </summary>
		public static void Select(GameObject target)
		{
			if (target == null)
			{
				return;
			}

			EventSystem owner = GetOwner(target);
			if (owner != null && !owner.alreadySelecting)
			{
				owner.SetSelectedGameObject(target);
			}
		}

		/// <summary>
		/// The player event system whose root contains <paramref name="target"/>, else the global current one.
		/// </summary>
		public static EventSystem GetOwner(GameObject target)
		{
			foreach (MultiplayerEventSystem eventSystem in
				Object.FindObjectsByType<MultiplayerEventSystem>(FindObjectsSortMode.None))
			{
				if (eventSystem.isActiveAndEnabled && eventSystem.playerRoot != null &&
					target.transform.IsChildOf(eventSystem.playerRoot.transform))
				{
					return eventSystem;
				}
			}
			return EventSystem.current;
		}
	}
}
