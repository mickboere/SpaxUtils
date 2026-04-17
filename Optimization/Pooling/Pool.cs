using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class Pool : IService
	{
		/// <summary>
		/// The prefab this pool manages.
		/// </summary>
		public GameObject Prefab { get; protected set; }
	}

	/// <summary>
	/// Generic pool service that can manage a collection of <see cref="IPooledItem"/> instances.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class Pool<T> : Pool where T : MonoBehaviour, IPooledItem
	{
		public const int DEFAULT_SIZE = 15;
		public const bool DEFAULT_DYNAMIC = true;

		private readonly T prefab;
		private readonly bool dynamic;
		private Transform collectionParent;
		private readonly List<T> inactive;
		private readonly List<T> active;
		private readonly int initialSize;

		public Pool() : this(GlobalDependencyManager.Instance.Get<PooledItemDatabase>(), GlobalDependencyManager.Instance.Get<CallbackService>()) { }

		public Pool(PooledItemDatabase database, CallbackService callbackService) : this(database.GetPrefab<T>(), database.GetPrefab<T>().DefaultPoolSize, DEFAULT_DYNAMIC, callbackService) { }

		public Pool(T prefab, int size, bool dynamic, CallbackService callbackService = null)
		{
			if (prefab == null)
			{
				SpaxDebug.Error("Pool could be created:", $"No prefab for type \"{typeof(T).FullName}\" was given.");
				return;
			}

			Prefab = prefab.gameObject;
			this.prefab = prefab;
			this.dynamic = dynamic;
			initialSize = Mathf.Max(0, size);

			inactive = new List<T>(initialSize);
			active = new List<T>(initialSize);

			EnsureValidPool(warmToInitialSize: true);

			// Subscribe to updates for pinging whether items are finished.
			if (callbackService != null)
			{
				callbackService.UpdateCallback += OnUpdate;
			}
		}

		/// <summary>
		/// Ensures the pool root exists, removes dead references and repopulates up to the initial size.
		/// Useful to call proactively after scene load.
		/// </summary>
		public void Warm()
		{
			EnsureValidPool(warmToInitialSize: true);
		}

		public T Request(Transform parent = null, Action<T> onWillActivate = null)
		{
			return Request(Vector3.zero, parent, onWillActivate);
		}

		public T Request(Vector3 position, Transform parent = null, Action<T> onWillActivate = null)
		{
			return Request(position, Quaternion.identity, parent, onWillActivate);
		}

		/// <summary>
		/// Request an item from the pool and activate it.
		/// </summary>
		/// <param name="position">The position to place the item at in world space.</param>
		/// <param name="rotation">The rotation of the item in world space.</param>
		/// <param name="parent">The transform to set as the item's parent.</param>
		/// <returns>An active instance of type <typeparamref name="T"/>.</returns>
		public T Request(Vector3 position, Quaternion rotation, Transform parent = null, Action<T> onWillActivate = null)
		{
			EnsureValidPool(warmToInitialSize: true);

			if (inactive.Count > 0)
			{
				// Item(s) are available, return the first.
				T item = inactive[0];
				Claim(item, position, rotation, parent, onWillActivate);
				return item;
			}
			else if (dynamic)
			{
				// No items are available but the pool is dynamic, instantiate a new item and return it.
				T item = Instantiate();
				Claim(item, position, rotation, parent, onWillActivate);
				return item;
			}

			// No items are available and the pool isn't dynamic, return null.
			SpaxDebug.Error($"Pool request for \"{typeof(T).FullName}\" could not be fulfilled.", $"Either increase the size, make the pool dynamic or implement a solution where the oldest active item can be reclaimed.");
			return null;
		}

		/// <summary>
		/// Return an active item to the inactive pool.
		/// </summary>
		/// <param name="item">The item to return.</param>
		public void Return(T item)
		{
			if (item == null)
			{
				return;
			}

			EnsureValidPool(warmToInitialSize: false);

			if (!active.Remove(item))
			{
				if (inactive.Contains(item))
				{
					return;
				}
			}

			inactive.Add(item);
			item.transform.SetParent(collectionParent);
			item.gameObject.SetActive(false);
		}

		private void OnUpdate()
		{
			// Poll all active items to see if they are finished. If they are, return them.
			for (int i = 0; i < active.Count; i++)
			{
				T item = active[i];

				if (item == null)
				{
					active.RemoveAt(i);
					i--;
					continue;
				}

				if (item.Finished)
				{
					Return(item);
					i--;
				}
			}
		}

		private void EnsureValidPool(bool warmToInitialSize)
		{
			if (collectionParent == null)
			{
				collectionParent = new GameObject($"Pool<{typeof(T).FullName}>({Prefab.name})").transform;
			}

			inactive.RemoveAll(item => item == null);
			active.RemoveAll(item => item == null);

			if (warmToInitialSize)
			{
				int missing = initialSize - (inactive.Count + active.Count);

				for (int i = 0; i < missing; i++)
				{
					Instantiate();
				}
			}
		}

		private T Instantiate()
		{
			// Instantiate a new item and add it to the inactive list.
			T item = UnityEngine.Object.Instantiate(prefab, collectionParent);
			item.gameObject.SetActive(false);
			inactive.Add(item);
			return item;
		}

		private void Claim(T item, Vector3 position, Quaternion rotation, Transform parent, Action<T> onWillActivate = null)
		{
			// Claim an inactive item and make it active.
			active.Add(item);
			inactive.Remove(item);
			item.transform.SetPositionAndRotation(position, rotation);
			item.transform.SetParent(parent);
			onWillActivate?.Invoke(item);
			item.gameObject.SetActive(true);
		}
	}
}
