using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Service that keeps track of which <see cref="IWorldRegion"/> a transform is contained in.
	/// Subscribers are notified when their current highest-priority region changes.
	/// </summary>
	public class WorldRegionService : IService
	{
		private List<IWorldRegion> regions = new List<IWorldRegion>();
		private Dictionary<Transform, List<Action<IWorldRegion>>> subscribers = new Dictionary<Transform, List<Action<IWorldRegion>>>();
		private Dictionary<Transform, IWorldRegion> register = new Dictionary<Transform, IWorldRegion>();

		public WorldRegionService(CallbackService callbackService)
		{
			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		private void OnUpdate(float delta)
		{
			// Copy keys to avoid issues if a callback modifies the subscriber list.
			List<Transform> transforms = new List<Transform>(subscribers.Keys);

			foreach (Transform t in transforms)
			{
				IWorldRegion newRegion = GetRegion(t.position);

				if (newRegion != register[t])
				{
					register[t] = newRegion;

					List<Action<IWorldRegion>> callbacks = subscribers[t];
					for (int i = 0; i < callbacks.Count; i++)
					{
						callbacks[i](newRegion);
					}
				}
			}
		}

		/// <summary>
		/// Registers a region to be tracked by this service.
		/// </summary>
		public void Register(IWorldRegion region)
		{
			if (!regions.Contains(region))
			{
				regions.Add(region);
			}
		}

		/// <summary>
		/// Removes a region from this service.
		/// </summary>
		public void Remove(IWorldRegion region)
		{
			regions.Remove(region);
		}

		/// <summary>
		/// Returns the highest-priority region that contains <paramref name="point"/>, or null if none.
		/// </summary>
		public IWorldRegion GetRegion(Vector3 point)
		{
			IWorldRegion highest = null;

			foreach (IWorldRegion region in regions)
			{
				if (region.IsInside(point) && (highest == null || region.Prio > highest.Prio))
				{
					highest = region;
				}
			}

			return highest;
		}

		/// <summary>
		/// Returns the highest-priority region that contains <paramref name="transform"/>.
		/// If the transform is a subscriber, returns the cached registered value.
		/// </summary>
		public IWorldRegion GetRegion(Transform transform)
		{
			if (register.ContainsKey(transform))
			{
				return register[transform];
			}

			return GetRegion(transform.position);
		}

		/// <summary>
		/// Subscribes <paramref name="transform"/> to region-change notifications.
		/// <paramref name="callback"/> is invoked whenever the transform moves into a different region.
		/// </summary>
		public void Subscribe(Transform transform, Action<IWorldRegion> callback)
		{
			if (subscribers.ContainsKey(transform))
			{
				subscribers[transform].Add(callback);
			}
			else
			{
				subscribers[transform] = new List<Action<IWorldRegion>>() { callback };
				register[transform] = GetRegion(transform.position);
			}
		}

		/// <summary>
		/// Removes a region-change subscription from <paramref name="transform"/>.
		/// </summary>
		public void Unsubscribe(Transform transform, Action<IWorldRegion> callback)
		{
			if (!subscribers.ContainsKey(transform))
			{
				return;
			}

			subscribers[transform].Remove(callback);

			if (subscribers[transform].Count == 0)
			{
				subscribers.Remove(transform);
				register.Remove(transform);
			}
		}
	}
}
