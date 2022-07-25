using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// Class that keeps track of <see cref="IEntity"/>s within the <see cref="IEntityCollection"/> of type <typeparamref name="T"/> and definable evaluation.
	/// </summary>
	public class EntityFilter<T> : IDisposable where T : class
	{
		public event Action<T> AddedEntityEvent;
		public event Action<T> RemovedEntityEvent;

		public List<T> Entities
		{
			get
			{
				if (entities == null)
				{
					entities = entityCollection.Get<T>(evaluate, exclude);
				}
				return entities;
			}
		}
		public int Count => Entities.Count;

		protected Func<T, bool> evaluate;
		protected IEntity[] exclude;
		protected IEntityCollection entityCollection;

		private List<T> entities;

		/// <summary>
		/// Creates a new type-only <see cref="IEntity"/> filter.
		/// </summary>
		public EntityFilter(IEntityCollection entityCollection, params IEntity[] exclude) : this(entityCollection, (e) => true, exclude)
		{
		}

		/// <summary>
		/// Creates a new custom <see cref="IEntity"/> filter.
		/// </summary>
		public EntityFilter(IEntityCollection entityCollection, Func<T, bool> evaluateFunc, params IEntity[] exclude)
		{
			this.entityCollection = entityCollection;
			evaluate = evaluateFunc;
			this.exclude = exclude;

			// The entities aren't retrieved until we require a reference to them.
			// This is because in some cases the evaluate func is set in a higher implementation, after the base construction.

			entityCollection.AddedEntityEvent += OnAddedEntity;
			entityCollection.RemovedEntityEvent += OnRemovedEntity;
		}

		protected virtual void OnAddedEntity(IEntity entity)
		{
			if (!exclude.Contains(entity) && entity is T cast && evaluate(cast))
			{
				AddEntity(cast);
			}
		}

		protected virtual void OnRemovedEntity(IEntity entity)
		{
			if (entity is T cast && Entities.Contains(cast))
			{
				RemoveEntity(cast);
			}
		}

		protected virtual void AddEntity(T entity)
		{
			Entities.Add(entity);
			AddedEntityEvent?.Invoke(entity);
		}

		protected virtual void RemoveEntity(T entity)
		{
			Entities.Remove(entity);
			RemovedEntityEvent?.Invoke(entity);
		}

		public virtual void Dispose()
		{
			entityCollection.AddedEntityEvent -= OnAddedEntity;
			entityCollection.RemovedEntityEvent -= OnRemovedEntity;
			Entities.Clear();
		}
	}
}
