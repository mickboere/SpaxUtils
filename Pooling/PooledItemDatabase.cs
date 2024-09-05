using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Database asset containing all (generic) pooled item prefabs.
	/// </summary>
	[CreateAssetMenu(fileName = "PooledItemDatabase", menuName = "ScriptableObjects/PooledItemDatabase")]
	public class PooledItemDatabase : ScriptableObject
	{
		[SerializeField] private List<PooledItemBase> pooledItems;

		private Dictionary<Type, PooledItemBase> cache = new Dictionary<Type, PooledItemBase>();

		/// <summary>
		/// Retrieve pool prefab for <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">The type of (<see cref="PooledItemBase"/>) prefab to return.</typeparam>
		/// <returns></returns>
		public T GetPrefab<T>() where T : MonoBehaviour, IPooledItem
		{
			Type type = typeof(T);
			if (cache.ContainsKey(type))
			{
				return cache[type] as T;
			}

			foreach (PooledItemBase item in pooledItems)
			{
				if (item is T)
				{
					cache.Add(type, item);
					return item as T;
				}
			}

			return null;
		}
	}
}
