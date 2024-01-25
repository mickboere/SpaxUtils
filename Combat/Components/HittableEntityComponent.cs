using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IHittable"/> implementation that passes along incoming hits.
	/// </summary>
	public class HittableEntityComponent : EntityComponentBase, IHittable
	{
		/// <inheritdoc/>
		public bool Hittable => hittable;

		[SerializeField] private bool hittable = true;

		private List<(object listener, Action<HitData> callback, int prio)> subscribers =
			new List<(object listener, Action<HitData> callback, int prio)>();

		/// <inheritdoc/>
		public bool Hit(HitData hitData)
		{
			if (Hittable)
			{
				for (int i = 0; i < subscribers.Count; i++)
				{
					subscribers[i].callback.Invoke(hitData);
				}
			}

			return Hittable;
		}

		/// <inheritdoc/>
		public void Subscribe(object listener, Action<HitData> callback, int prio = 0)
		{
			subscribers.Add((listener, callback, prio));
			if (subscribers.Count > 2 && prio > subscribers[subscribers.Count - 2].prio)
			{
				subscribers.Sort((a, b) => a.prio.CompareTo(b.prio));
			}
		}

		/// <inheritdoc/>
		public void Unsubscribe(object listener)
		{
			for (int i = 0; i < subscribers.Count; i++)
			{
				if (subscribers[i].listener == listener)
				{
					subscribers.RemoveAt(i);
					i--;
				}
			}
		}
	}
}
