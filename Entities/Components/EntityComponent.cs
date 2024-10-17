using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Base implementation for <see cref="IEntityComponent"/>.
	/// </summary>
	public abstract class EntityComponent : IEntityComponent
	{
		public IEntity Entity { get; private set; }

		public GameObject GameObject => Entity.GameObject;
		public Transform Transform => Entity.GameObject.transform;

		protected IDependencyManager DependencyManager { get; private set; }
		protected virtual bool AutoInject => true;
		protected EntityStat EntityTimeScale { get; private set; }

		public EntityComponent(IEntity entity, IDependencyManager dependencyManager)
		{
			Entity = entity;
			EntityTimeScale = entity.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);
			DependencyManager = dependencyManager;
			if (AutoInject)
			{
				dependencyManager.Inject(this);
			}
		}
	}
}
