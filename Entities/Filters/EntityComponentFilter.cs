using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// Class that keeps track of <see cref="IEntityComponent"/>s found within the <see cref="IEntityCollection"/> of type <typeparamref name="T"/> and definable evaluations.
	/// </summary>
	/// <typeparam name="T">The type of <see cref="IEntityComponent"/></typeparam>
	public class EntityComponentFilter<T> : IEntityComponentFilter<T> where T : class, IEntityComponent
	{
		public event Action<T> AddedComponentEvent;
		public event Action<T> RemovedComponentEvent;

		public List<T> Components
		{
			get
			{
				if (_components == null)
				{
					_components = entityCollection.GetComponents(evaluateEntity, evaluateComponent, exclude);
				}
				return _components;
			}
			private set
			{
				_components = value;
			}
		}

		protected Func<IEntity, bool> evaluateEntity;
		protected Func<T, bool> evaluateComponent;
		protected IEntity[] exclude;
		protected IEntityCollection entityCollection;

		private List<T> _components;

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

		public void Reevaluate()
		{
			List<T> old = new List<T>(Components);
			Components = entityCollection.GetComponents(evaluateEntity, evaluateComponent, exclude);
			List<T> removed = old.Except(Components).ToList();
			List<T> added = Components.Except(old).ToList();
			foreach (T r in removed)
			{
				RemovedComponentEvent?.Invoke(r);
			}
			foreach (T a in added)
			{
				AddedComponentEvent?.Invoke(a);
			}
		}

		protected virtual void OnAddedEntity(IEntity entity)
		{
			if (evaluateEntity(entity) &&
				entity.TryGetEntityComponent(out T component) &&
				!exclude.Contains(component.Entity) && // Through shared dependencies, some entities can receive components from parent entities.
				evaluateComponent(component))
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
		}
	}
}
