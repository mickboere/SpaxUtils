using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Service that keeps track of which <see cref="IWorldRegion"/> a transform/point is contained in.
	/// </summary>
	public class WorldService : IService
	{
		List<IWorldRegion> regions = new List<IWorldRegion>();
		Dictionary<Transform, List<Action<IWorldRegion>>> subscribers = new Dictionary<Transform, List<Action<IWorldRegion>>>();
		Dictionary<Transform, IWorldRegion> register = new Dictionary<Transform, IWorldRegion>();

		public WorldService(CallbackService callbackService)
		{
			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		private void OnUpdate(float delta)
		{
			foreach (Transform transform in subscribers.Keys)
			{
				// First check if subscriber entered a higher priority region than current.
				IWorldRegion highest = register[transform];
				List<IWorldRegion> inside = new List<IWorldRegion>();
				if (highest != null) { inside.Add(highest); }
				foreach (IWorldRegion region in regions)
				{
					if ((region.HasEntry && region.HasExit && region.HasEntered(transform.position) && !region.HasExited(transform.position)) ||
						(region.HasEntry && !region.HasExit && region.HasEntered(transform.position)) ||
						(!region.HasEntry && region.IsInside(transform.position)))
					{
						if (highest == null || region.Prio > highest.Prio)
						{
							// New highest.
							highest = region;
							inside.Add(region);
						}
						else
						{
							// Insert on prio list.
							for (int i = 0; i < inside.Count; i++)
							{
								if (region.Prio < inside[i].Prio)
								{
									inside.Insert(i, region);
									break;
								}
							}
						}
					}
				}
				if (highest == null)
				{
					// Inside no region.
					register[transform] = null;
					continue;
				}

				// Check if highest has been exited
				if ((highest.HasExit && highest.HasExited(transform.position)) ||
					(!highest.HasExit && !highest.IsInside(transform.position)))
				{
					int i = inside.IndexOf(highest) - 1;
					if (i < 0)
					{
						highest = null;
					}
					else
					{
						highest = inside[i];
					}
				}

				// Invoke subscribers with new highest prio region.
				if (highest != register[transform])
				{
					foreach (Action<IWorldRegion> subscriber in subscribers[transform])
					{
						subscriber(highest);
					}
				}

				register[transform] = highest;
			}
		}

		/// <summary>
		/// Register a new world region to be included.
		/// </summary>
		public void Register(IWorldRegion region)
		{
			if (!regions.Contains(region))
			{
				regions.Add(region);
			}
		}

		/// <summary>
		/// Removes a world region to no longer be included.
		/// </summary>
		public void Remove(IWorldRegion region)
		{
			if (regions.Contains(region))
			{
				regions.Remove(region);
			}
		}

		/// <summary>
		/// Returns which region <paramref name="point"/> is contained in.
		/// Does not take into account entry- and exit-zones.
		/// </summary>
		public IWorldRegion GetRegion(Vector3 point)
		{
			IWorldRegion highest = null;
			int prio = int.MinValue;
			foreach (IWorldRegion region in regions)
			{
				if (region.Prio > prio && region.IsInside(point))
				{
					highest = region;
				}
			}
			return highest;
		}

		/// <summary>
		/// Returns which region <paramref name="transform"/> is contained in.
		/// If the <paramref name="transform"/> is a subscriber, the region contained within the registry will be returned, meaning entry- and exit-zones are taken into account.
		/// </summary>
		public IWorldRegion GetRegion(Transform transform)
		{
			if (register.ContainsKey(transform))
			{
				return register[transform];
			}
			else
			{
				return GetRegion(transform.position);
			}
		}

		/// <summary>
		/// Registers <paramref name="transform"/> to be within <paramref name="region"/>.
		/// </summary>
		private void Register(Transform transform, IWorldRegion region)
		{
			register[transform] = region;
		}

		/// <summary>
		/// A a new region-change subscriber for <paramref name="transform"/>.
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
			}
			register[transform] = GetRegion(transform);
		}

		/// <summary>
		/// Removes a region-change subscriber from <paramref name="transform"/>.
		/// </summary>
		public void Unsubscribe(Transform transform, Action<IWorldRegion> callback)
		{
			subscribers[transform].Remove(callback);
			if (subscribers[transform].Count == 0)
			{
				subscribers.Remove(transform);
				register.Remove(transform);
			}
		}
	}
}
