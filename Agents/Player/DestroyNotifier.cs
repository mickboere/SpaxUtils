using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Invokes <see cref="DestroyedEvent"/> when its GameObject is destroyed.
	/// </summary>
	public class DestroyNotifier : MonoBehaviour
	{
		public event Action DestroyedEvent;

		/// <summary>
		/// Returns the notifier on <paramref name="gameObject"/>, adding one if needed.
		/// </summary>
		public static DestroyNotifier Get(GameObject gameObject)
		{
			if (!gameObject.TryGetComponent(out DestroyNotifier notifier))
			{
				notifier = gameObject.AddComponent<DestroyNotifier>();
			}
			return notifier;
		}

		protected void OnDestroy()
		{
			DestroyedEvent?.Invoke();
			DestroyedEvent = null;
		}
	}
}
