using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Generic pool service that can manage a collection of <see cref="IPooledItem"/> instances.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class Pool<T> : IService where T : MonoBehaviour, IPooledItem
	{
		public const int DEFAULT_SIZE = 15;
		public const bool DEFAULT_DYNAMIC = true;

		private T prefab;
		private bool dynamic;
		private Transform collectionParent;
		private List<T> inactive;
		private List<T> active;

		public Pool() : this(GlobalDependencyManager.Instance.Get<PooledItemDatabase>(), GlobalDependencyManager.Instance.Get<CallbackService>()) { }

		public Pool(PooledItemDatabase database, CallbackService callbackService) : this(database.GetPrefab<T>(), database.GetPrefab<T>().DefaultPoolSize, DEFAULT_DYNAMIC, callbackService) { }

		public Pool(T prefab, int size, bool dynamic, CallbackService callbackService = null)
		{
			if (prefab == null)
			{
				SpaxDebug.Error("Pool could be created:", $"No prefab for type \"{typeof(T).FullName}\" was given.");
			}

			this.prefab = prefab;
			this.dynamic = dynamic;
			collectionParent = new GameObject($"Pool<{typeof(T).FullName}>").transform;

			inactive = new List<T>();
			active = new List<T>();

			// Populate pool.
			for (int i = 0; i < size; i++)
			{
				Instantiate();
			}

			// Subscribe to updates for pinging whether items are finished.
			if (callbackService != null)
			{
				callbackService.UpdateCallback += OnUpdate;
			}
		}

		public T Request(Transform parent = null)
		{
			return Request(Vector3.zero, parent);
		}

		public T Request(Vector3 position, Transform parent = null)
		{
			return Request(position, Quaternion.identity, parent);
		}

		/// <summary>
		/// Request an item from the pool and activate it.
		/// </summary>
		/// <param name="position">The position to place the item at in world space.</param>
		/// <param name="rotation">The rotation of the item in world space.</param>
		/// <param name="parent">The transform to set as the item's parent.</param>
		/// <returns>An active instance of type <typeparamref name="T"/>.</returns>
		public T Request(Vector3 position, Quaternion rotation, Transform parent = null)
		{
			if (inactive.Count > 0 || dynamic)
			{
				// Item(s) are available, return the first.
				T item = inactive[0];
				Claim(item, position, rotation, parent);
				return item;
			}
			else if (dynamic)
			{
				// No items are available but the pool is dynamic, instantiate a new item and return it.
				T item = Instantiate();
				Claim(item, position, rotation, parent);
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
			inactive.Add(item);
			active.Remove(item);
			item.transform.SetParent(collectionParent);
			item.gameObject.SetActive(false);
		}

		private void OnUpdate()
		{
			// Pol all active items to see if they are finished. If they are, return them.
			for (int i = 0; i < active.Count; i++)
			{
				if (active[i].Finished)
				{
					Return(active[i]);
					i--;
				}
			}
		}

		private T Instantiate()
		{
			// Instantiate a new item and add it to the inactive list.
			T item = Object.Instantiate(prefab, collectionParent);
			item.gameObject.SetActive(false);
			inactive.Add(item);
			return item;
		}

		private void Claim(T item, Vector3 position, Quaternion rotation, Transform parent)
		{
			// Claim an inactive item and make it active.
			active.Add(item);
			inactive.Remove(item);
			item.transform.position = position;
			item.transform.rotation = rotation;
			item.transform.SetParent(parent);
			item.gameObject.SetActive(true);
		}
	}
}
