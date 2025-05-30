using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Service that manages pools of a specific prefab type rather than <see cref="IPooledItem"/> implementation.
	/// Used for different (effect) prefabs implementing the same <see cref="IPooledItem"/> type, requiring different pools.
	/// </summary>
	public class GlobalPoolingManager : IService
	{
		private CallbackService callbackService;

		private Dictionary<IPooledItem, Pool> pools = new Dictionary<IPooledItem, Pool>();

		public GlobalPoolingManager(CallbackService callbackService)
		{
			this.callbackService = callbackService;
		}

		public Pool<T> Request<T>(T prefab) where T : MonoBehaviour, IPooledItem
		{
			if (pools.ContainsKey(prefab))
			{
				return (Pool<T>)pools[prefab];
			}

			Pool<T> pool = new Pool<T>(prefab, prefab.DefaultPoolSize, Pool<T>.DEFAULT_DYNAMIC, callbackService);
			pools.Add(prefab, pool);
			return pool;
		}
	}
}
