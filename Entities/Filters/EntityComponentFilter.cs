using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// Class that keeps track of <see cref="IEntityComponent"/>s found within the <see cref="IEntityCollection"/> of type <typeparamref name="T"/> and definable evaluations.
	/// </summary>
	/// <typeparam name="T">The type of <see cref="IEntityComponent"/></typeparam>
	public class EntityComponentFilter<T> : IDisposable where T : class, IEntityComponent
	{
		public event Action<T> AddedComponentEvent;
		public event Action<T> RemovedComponentEvent;

		public List<T> Components
		{
			get
			{
				if (components == null)
				{
					components = entityCollection.GetComponents<T>(evaluateEntity, evaluateComponent, exclude);
				}
				return components;
			}
		}
		public int Count => Components.Count;

		protected Func<IEntity, bool> evaluateEntity;
		protected Func<T, bool> evaluateComponent;
		protected IEntity[] exclude;
		protected IEntityCollection entityCollection;

		private List<T> components;

		/// <summary>
		/// Creates a new type-only <see cref="IEntityComponent"/> filter.
		/// </summary>
		public EntityComponentFilter(IEntityCollection entityCollection, params IEntity[] exclude) : this(entityCollection, (e) => true, (c) => true, exclude)
		{
		}

		/// <summary>
		/// Creates a new custom <see cref="IEntityComponent"/> filter.
		/// </summary>
		public EntityComponentFilter(IEntityCollection entityCollection, Func<IEntity, bool> evaluateEntity, Func<T, bool> evaluateComponent, params IEntity[] exclude)
		{
			this.entityCollection = entityCollection;
			this.evaluateEntity = evaluateEntity;
			this.evaluateComponent = evaluateComponent;
			this.exclude = exclude;

			// The entity components aren't retrieved until we require a reference to them.
			// This is because in some cases the evaluate func is set in a higher implementation, after the base construction.

			entityCollection.AddedEntityEvent += OnAddedEntity;
			entityCollection.RemovedEntityEvent += OnRemovedEntity;
		}

		protected virtual void OnAddedEntity(IEntity entity)
		{
			if (!exclude.Contains(entity) && evaluateEntity(entity) && entity.TryGetEntityComponent(out T component) && evaluateComponent(component))
			{
				AddComponent(component);
			}
		}

		protected virtual void OnRemovedEntity(IEntity entity)
		{
			if (entity.TryGetEntityComponent(out T component) && Components.Contains(component))
			{
				RemoveComponent(component);
			}
		}

		protected virtual void AddComponent(T component)
		{
			Components.Add(component);
			AddedComponentEvent?.Invoke(component);
		}

		protected virtual void RemoveComponent(T component)
		{
			Components.Remove(component);
			RemovedComponentEvent?.Invoke(component);
		}

		public virtual void Dispose()
		{
			entityCollection.AddedEntityEvent -= OnAddedEntity;
			entityCollection.RemovedEntityEvent -= OnRemovedEntity;
			Components.Clear();
		}
	}
}
