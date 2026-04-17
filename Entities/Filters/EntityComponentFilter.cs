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
				EnsureInitialized();
				return components;
			}
			private set
			{
				components = value;
			}
		}

		private List<T> components;
		private Dictionary<IEntity, T> byEntity;

		protected Func<IEntity, bool> evaluateEntity;
		protected Func<T, bool> evaluateComponent;
		protected IEntity[] exclude;
		protected IEntityCollection entityCollection;

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

			// Lazy init: we only build the initial list when needed or when the first event arrives.
			entityCollection.AddedEntityEvent += OnAddedEntity;
			entityCollection.RemovedEntityEvent += OnRemovedEntity;
		}

		public void Reevaluate()
		{
			EnsureInitialized();

			Dictionary<IEntity, T> oldMap = new Dictionary<IEntity, T>(byEntity);
			List<T> newComponents = entityCollection.GetComponents(evaluateEntity, evaluateComponent, exclude);

			Dictionary<IEntity, T> newMap = new Dictionary<IEntity, T>();
			for (int i = 0; i < newComponents.Count; i++)
			{
				T c = newComponents[i];
				if (c == null)
				{
					continue;
				}

				IEntity e = c.Entity;
				if (e == null)
				{
					continue;
				}

				if (!newMap.ContainsKey(e))
				{
					newMap.Add(e, c);
				}
			}

			// Removed: in oldMap but not in newMap.
			foreach (KeyValuePair<IEntity, T> kv in oldMap)
			{
				if (!newMap.ContainsKey(kv.Key))
				{
					RemovedComponentEvent?.Invoke(kv.Value);
				}
			}

			// Added: in newMap but not in oldMap.
			foreach (KeyValuePair<IEntity, T> kv in newMap)
			{
				if (!oldMap.ContainsKey(kv.Key))
				{
					AddedComponentEvent?.Invoke(kv.Value);
				}
			}

			components = newComponents;
			byEntity = newMap;
		}

		protected virtual void OnAddedEntity(IEntity entity)
		{
			EnsureInitialized();

			if (entity == null)
			{
				return;
			}

			if (exclude != null && exclude.Contains(entity))
			{
				return;
			}

			if (!evaluateEntity(entity))
			{
				return;
			}

			if (!entity.TryGetEntityComponent(out T component))
			{
				return;
			}

			if (component == null)
			{
				return;
			}

			if (exclude != null && exclude.Contains(component.Entity))
			{
				return;
			}

			if (!evaluateComponent(component))
			{
				return;
			}

			if (!byEntity.ContainsKey(entity))
			{
				byEntity.Add(entity, component);
				components.Add(component);
				AddedComponentEvent?.Invoke(component);
			}
		}

		protected virtual void OnRemovedEntity(IEntity entity)
		{
			EnsureInitialized();

			if (entity == null)
			{
				return;
			}

			if (byEntity.TryGetValue(entity, out T component))
			{
				byEntity.Remove(entity);
				components.Remove(component);
				RemovedComponentEvent?.Invoke(component);
			}
		}

		public virtual void Dispose()
		{
			entityCollection.AddedEntityEvent -= OnAddedEntity;
			entityCollection.RemovedEntityEvent -= OnRemovedEntity;
		}

		private void EnsureInitialized()
		{
			if (components != null && byEntity != null)
			{
				return;
			}

			components = entityCollection.GetComponents(evaluateEntity, evaluateComponent, exclude);

			byEntity = new Dictionary<IEntity, T>();
			for (int i = 0; i < components.Count; i++)
			{
				T c = components[i];
				if (c == null)
				{
					continue;
				}

				IEntity e = c.Entity;
				if (e == null)
				{
					continue;
				}

				if (!byEntity.ContainsKey(e))
				{
					byEntity.Add(e, c);
				}
			}
		}
	}
}
