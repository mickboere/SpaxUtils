using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Base implementation for <see cref="IEntityComponent"/>.
	/// </summary>
	public abstract class EntityComponentBase : MonoBehaviour, IEntityComponent
	{
		public GameObject GameObject => Entity.GameObject;
		public Transform Transform => Entity.GameObject.transform;

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
			EntityTimeScale = entity.GetStat(EntityStatIdentifier.TIMESCALE, true, 1f);
		}
	}
}