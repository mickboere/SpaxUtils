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

		private List<(object listener, Action<HitData> callback, int order)> subscribers =
			new List<(object listener, Action<HitData> callback, int order)>();

		/// <inheritdoc/>
		public bool Hit(HitData hitData)
		{
			// Captured up front: a killing blow turns hittability off mid-loop, but the hit itself still landed.
			bool accepted = IsHittable;

			if (accepted)
			{
				for (int i = 0; i < subscribers.Count; i++)
				{
					subscribers[i].callback.Invoke(hitData);
				}
			}

			return accepted;
		}

		/// <inheritdoc/>
		public void Subscribe(object listener, Action<HitData> callback, int order = 0)
		{
			// Ordered insert; equal orders keep their subscription order.
			int index = subscribers.Count;
			while (index > 0 && subscribers[index - 1].order > order)
			{
				index--;
			}
			subscribers.Insert(index, (listener, callback, order));
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
