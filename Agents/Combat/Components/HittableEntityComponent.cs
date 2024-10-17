using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IHittable"/> implementation that passes along incoming hits.
	/// </summary>
	public class HittableEntityComponent : EntityComponentMono, IHittable
	{
		/// <inheritdoc/>
		public bool IsHittable { get { return hittable; } set { hittable = value; } }

		[SerializeField] private bool hittable = true;

		private List<(object listener, Action<HitData> callback, int prio)> subscribers =
			new List<(object listener, Action<HitData> callback, int prio)>();

		/// <inheritdoc/>
		public bool Hit(HitData hitData)
		{
			if (IsHittable)
			{
				for (int i = 0; i < subscribers.Count; i++)
				{
					subscribers[i].callback.Invoke(hitData);
				}
			}

			return IsHittable;
		}

		/// <inheritdoc/>
		public void Subscribe(object listener, Action<HitData> callback, int prio = 0)
		{
			subscribers.Add((listener, callback, prio));
			if (subscribers.Count > 2 && prio > subscribers[subscribers.Count - 2].prio)
			{
				subscribers.Sort((a, b) => b.prio.CompareTo(a.prio));
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
