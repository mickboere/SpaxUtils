using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Base implementation for <see cref="IEntityComponent"/>.
	/// </summary>
	public abstract class EntityComponent : IEntityComponent
	{
		/// <inheritdoc/>
		public IEntity Entity { get; private set; }

		public GameObject GameObject => Entity.GameObject;
		public Transform Transform => Entity.GameObject.transform;

		/// <summary>
		/// The dependency manager responsible for injecting this component's dependencies.
		/// </summary>
		protected IDependencyManager DependencyManager { get; private set; }

		/// <summary>
		/// Whether this entity component should automatically have its dependencies injected upon construction.
		/// </summary>
		protected virtual bool AutoInject => true;

		/// <summary>
		/// The timescale stat of the entity.
		/// </summary>
		protected EntityStat EntityTimeScale { get; private set; }

		public EntityComponent(IEntity entity, IDependencyManager dependencyManager)
		{
			Entity = entity;
			EntityTimeScale = entity.Stats.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);
			DependencyManager = dependencyManager;
			if (AutoInject)
			{
				dependencyManager.Inject(this);
			}
		}
	}
}
