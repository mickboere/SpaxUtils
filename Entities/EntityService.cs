using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Service keeping track of ALL active <see cref="IEntity"/>s.
	/// Implements <see cref="IEntityCollection"/>.
	/// </summary>
	public class EntityService : IEntityCollection, IService
	{
		public event Action<IEntity> AddedEntityEvent;
		public event Action<IEntity> RemovedEntityEvent;

		private Dictionary<string, IEntity> entities;

		public EntityService()
		{
			entities = new Dictionary<string, IEntity>();
		}

		/// <summary>
		/// Registers the given <see cref="IEntity"/>.
		/// </summary>
		public void Add(IEntity entity)
		{
			if (entity == null)
			{
				SpaxDebug.Error("Tried to add a null entity.");
				return;
			}

			string id = entity.ID;
			if (string.IsNullOrEmpty(id))
			{
				SpaxDebug.Error("Tried to add an entity with an empty ID.", context: entity is UnityEngine.Object uo ? uo : null);
				return;
			}

			if (entities.TryGetValue(id, out IEntity existing))
			{
				if (IsDestroyedUnityObject(existing))
				{
					SpaxDebug.Error($"EntityService contained a destroyed entity for ID \"{id}\". This violates the entity lifecycle contract. Overwriting entry.", context: entity is UnityEngine.Object ctx ? ctx : null);
					entities[id] = entity;
					SpaxDebug.Log("Added entity:", id, color: Color.darkGreen);
					AddedEntityEvent?.Invoke(entity);
					return;
				}

				if (existing != null)
				{
					SpaxDebug.Error($"An entity with ID \"{id}\" has already been registered!", context: entity is UnityEngine.Object go ? go : null);
					return;
				}
			}

			entities[id] = entity;
			AddedEntityEvent?.Invoke(entity);
		}

		/// <summary>
		/// Deregisters the given <see cref="IEntity"/>.
		/// </summary>
		public void Remove(IEntity entity)
		{
			if (entity == null)
			{
				return;
			}

			Remove(entity.ID);
		}

		/// <summary>
		/// Deregisters an entity by ID. This is the canonical removal path.
		/// </summary>
		public void Remove(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return;
			}

			if (entities.TryGetValue(id, out IEntity removed))
			{
				entities.Remove(id);
				if (removed != null)
				{
					RemovedEntityEvent?.Invoke(removed);
				}
			}
		}

		public T Get<T>(string id) where T : class, IEntity
		{
			if (entities.TryGetValue(id, out IEntity e))
			{
				if (IsDestroyedUnityObject(e))
				{
					SpaxDebug.Error($"EntityService.Get found destroyed entity for ID \"{id}\". Removing stale entry.");
					Remove(id);
					return null;
				}

				return (T)e;
			}

			return null;
		}

		public bool TryGet<T>(string id, out T entity) where T : class, IEntity
		{
			entity = Get<T>(id);
			return entity != null;
		}

		/// <summary>
		/// Returns entities implementing <typeparamref name="T"/>.
		/// </summary>
		public List<T> Get<T>(Func<T, bool> evaluation, params IEntity[] exclude) where T : class
		{
			List<T> filter = new List<T>();
			foreach (IEntity entity in entities.Values)
			{
				if (IsDestroyedUnityObject(entity))
				{
					continue;
				}

				if (!exclude.Contains(entity) && entity is T castedEntity && evaluation(castedEntity))
				{
					filter.Add(castedEntity);
				}
			}

			return filter;
		}

		/// <summary>
		/// Returns all entities implementing type <typeparamref name="T"/> except those that are <paramref name="exclude"/>d.
		/// </summary>
		public List<T> Get<T>(params IEntity[] exclude) where T : class
		{
			return Get<T>((e) => true, exclude);
		}

		/// <summary>
		/// Returns all entities whose <see cref="IIdentification"/> contains ALL of the given <paramref name="labels"/>.
		/// Returns an empty list if <paramref name="labels"/> is null or empty.
		/// </summary>
		public List<IEntity> GetByLabels(params string[] labels)
		{
			List<IEntity> result = new List<IEntity>();

			if (labels == null || labels.Length == 0)
			{
				return result;
			}

			foreach (IEntity entity in entities.Values)
			{
				if (IsDestroyedUnityObject(entity))
				{
					continue;
				}

				if (entity.Identification.HasAll(labels))
				{
					result.Add(entity);
				}
			}

			return result;
		}

		/// <summary>
		/// Returns all <see cref="IEntityComponent"/>s implementing <typeparamref name="T"/> of all tracked <see cref="IEntity"/>s.
		/// </summary>
		public List<T> GetComponents<T>(Func<IEntity, bool> entityEvaluation, Func<T, bool> componentEvaluation, params IEntity[] exclude) where T : class, IEntityComponent
		{
			List<T> components = new List<T>();
			List<IEntity> ex = exclude.ToList();

			foreach (IEntity entity in entities.Values)
			{
				if (IsDestroyedUnityObject(entity))
				{
					continue;
				}

				if (!ex.Contains(entity) &&
					entityEvaluation(entity) &&
					entity.TryGetEntityComponent(out T component) &&
					componentEvaluation(component))
				{
					components.Add(component);
				}
			}

			return components;
		}

		/// <summary>
		/// Returns all <see cref="IEntityComponent"/>s implementing <typeparamref name="T"/> of all tracked <see cref="IEntity"/>s.
		/// </summary>
		public List<T> GetComponents<T>(params IEntity[] exclude) where T : class, IEntityComponent
		{
			return GetComponents<T>((entity) => true, (component) => true, exclude);
		}

		private bool IsDestroyedUnityObject(object obj)
		{
			if (obj is UnityEngine.Object uo && !uo)
			{
				return true;
			}

			return false;
		}
	}
}
