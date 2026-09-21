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
		/// <summary>
		/// Invoked when a tracked entity moves from one region (first, may be null) into another (second, may be null).
		/// </summary>
		public event Action<IEntity, IWorldRegion, IWorldRegion> EntityRegionChangedEvent;

		private static readonly HashSet<IEntity> noOccupants = new HashSet<IEntity>();

		private List<IWorldRegion> regions = new List<IWorldRegion>();
		private Dictionary<Transform, List<Action<IWorldRegion>>> subscribers = new Dictionary<Transform, List<Action<IWorldRegion>>>();
		private Dictionary<Transform, IWorldRegion> register = new Dictionary<Transform, IWorldRegion>();

		// Two-way entity tracking, pinged at each entity's own optimization priority.
		private Dictionary<IEntity, Action<float>> tracked = new Dictionary<IEntity, Action<float>>();
		private Dictionary<IEntity, IWorldRegion> entityRegions = new Dictionary<IEntity, IWorldRegion>();
		private Dictionary<IWorldRegion, HashSet<IEntity>> occupants = new Dictionary<IWorldRegion, HashSet<IEntity>>();

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

			// Evict its occupants so no entity keeps pointing at a dead region.
			if (occupants.TryGetValue(region, out HashSet<IEntity> inside))
			{
				foreach (IEntity entity in new List<IEntity>(inside))
				{
					SetEntityRegion(entity, null);
				}
				occupants.Remove(region);
			}
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

		#region Entity Tracking

		/// <summary>
		/// Starts tracking <paramref name="entity"/>'s region, rechecked at the entity's optimization priority.
		/// </summary>
		public void Track(IEntity entity)
		{
			if (tracked.ContainsKey(entity))
			{
				return;
			}

			Action<float> ping = (delta) => SetEntityRegion(entity, GetRegion(entity.Transform.position));
			tracked.Add(entity, ping);
			entity.SubscribeOptimizedUpdate(ping);
			ping(0f);
		}

		/// <summary>
		/// Stops tracking <paramref name="entity"/> and removes it from its region's occupants.
		/// </summary>
		public void Untrack(IEntity entity)
		{
			if (!tracked.TryGetValue(entity, out Action<float> ping))
			{
				return;
			}

			entity.UnsubscribeOptimizedUpdate(ping);
			tracked.Remove(entity);
			SetEntityRegion(entity, null);
			entityRegions.Remove(entity);
		}

		/// <summary>
		/// Returns the region <paramref name="entity"/> was last found in, or null when outside all regions or untracked.
		/// </summary>
		public IWorldRegion GetRegion(IEntity entity)
		{
			return entityRegions.TryGetValue(entity, out IWorldRegion region) ? region : null;
		}

		/// <summary>
		/// Returns the tracked entities last found within <paramref name="region"/>.
		/// </summary>
		public IReadOnlyCollection<IEntity> GetOccupants(IWorldRegion region)
		{
			return region != null && occupants.TryGetValue(region, out HashSet<IEntity> inside) ? inside : noOccupants;
		}

		private void SetEntityRegion(IEntity entity, IWorldRegion region)
		{
			IWorldRegion previous = GetRegion(entity);
			if (previous == region && entityRegions.ContainsKey(entity))
			{
				return;
			}

			if (previous != null && occupants.TryGetValue(previous, out HashSet<IEntity> left))
			{
				left.Remove(entity);
			}
			if (region != null)
			{
				if (!occupants.TryGetValue(region, out HashSet<IEntity> entered))
				{
					entered = new HashSet<IEntity>();
					occupants.Add(region, entered);
				}
				entered.Add(entity);
			}

			entityRegions[entity] = region;
			if (previous != region)
			{
				EntityRegionChangedEvent?.Invoke(entity, previous, region);
			}
		}

		#endregion Entity Tracking
	}
}
