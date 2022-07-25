using System;
using System.Collections.Generic;
using System.Linq;

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

		private HashSet<IEntity> entities;

		public EntityService()
		{
			entities = new HashSet<IEntity>();
		}

		/// <summary>
		/// Registers the given <see cref="IEntity"/>.
		/// </summary>
		public void Add(IEntity entity)
		{
			entities.Add(entity);
			AddedEntityEvent?.Invoke(entity);
		}

		/// <summary>
		/// Deregisters the given <see cref="IEntity"/>.
		/// </summary>
		public void Remove(IEntity entity)
		{
			entities.Remove(entity);
			RemovedEntityEvent?.Invoke(entity);
		}

		/// <summary>
		/// Returns entities implementing <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">The type of <see cref="IEntity"/> implementation to look for.</typeparam>
		/// <param name="evaluation">Per-result <see cref="Func{T, bool}"/> evaluation.</param>
		/// <param name="exclude">The <see cref="IEntity"/>s to exclude from the results.</param>
		/// <returns>The resulting list of found entities.</returns>
		public List<T> Get<T>(Func<T, bool> evaluation, params IEntity[] exclude) where T : class
		{
			List<T> filter = new List<T>();
			foreach (IEntity entity in entities)
			{
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
		/// <typeparam name="T">The type of <see cref="IEntity"/> implementation to look for.</typeparam>
		/// <param name="exclude"></param>
		/// <returns>The <see cref="IEntity"/>s to exclude from the results.</returns>
		public List<T> Get<T>(params IEntity[] exclude) where T : class
		{
			return Get<T>((e) => true, exclude);
		}

		/// <summary>
		/// Returns all <see cref="IEntityComponent"/>s implementing <typeparamref name="T"/> of all tracked <see cref="IEntity"/>s.
		/// </summary>
		/// <typeparam name="T">The type of <see cref="IEntityComponent"/> implementations to look for.</typeparam>
		/// <param name="entityEvaluation">Per-result evaluation of the current <see cref="IEntity"/>.</param>
		/// <param name="componentEvaluation">Per-result evaluation of the current <see cref="IEntityComponent"/>.</param>
		/// <param name="exclude">The <see cref="IEntity"/>s to exclude from the results.</param>
		/// <returns></returns>
		public List<T> GetComponents<T>(Func<IEntity, bool> entityEvaluation, Func<T, bool> componentEvaluation, params IEntity[] exclude) where T : class, IEntityComponent
		{
			List<T> components = new List<T>();
			foreach (IEntity entity in entities)
			{
				if (!exclude.Contains(entity) && entityEvaluation(entity) && entity.TryGetEntityComponent(out T component) && componentEvaluation(component))
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
	}
}
