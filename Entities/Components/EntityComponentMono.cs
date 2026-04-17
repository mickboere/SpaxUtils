using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Base monobehaviour implementation for <see cref="IEntityComponent"/>.
	/// </summary>
	public abstract class EntityComponentMono : MonoBehaviour, IEntityComponent
	{
		public virtual GameObject GameObject => Entity.GameObject;
		public virtual Transform Transform => Entity.GameObject.transform;

		public IEntity Entity
		{
			get
			{
				if (entity == null)
				{
					entity = gameObject.GetComponentInParent<IEntity>();
				}

				return entity;
			}
		}

		protected EntityStat EntityTimeScale { get; private set; }

		private IEntity entity;

		public virtual void InjectDependencies(IEntity entity)
		{
			this.entity = entity;
			EntityTimeScale = entity.Stats.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);
		}
	}
}
