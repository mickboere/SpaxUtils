using System;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// Implementation of <see cref="ITargeter"/>.
	/// Stores an entity's current target as <see cref="ITargetable"/>.
	/// Also keeps track of the agent's friends and enemies as collections of <see cref="IEntityComponentFilter{ITargetable}"/>
	/// </summary>
	public class TargeterComponent : EntityComponentMono, ITargeter
	{
		/// <inheritdoc/>
		public event Action<ITargetable> TargetChangedEvent;

		#region Properties

		/// <inheritdoc/>
		public ITargetable Target { get; private set; }

		/// <inheritdoc/>
		public IEntityComponentFilter<ITargetable> Enemies => enemies;

		/// <inheritdoc/>
		public IEntityComponentFilter<ITargetable> Friends => friends;

		#endregion Properties

		private EntityComponentFilter<ITargetable> enemies;
		private EntityComponentFilter<ITargetable> friends;

		private IAgent agent;

		public void InjectDependencies(IAgent agent, IEntityCollection entityCollection)
		{
			this.agent = agent;

			enemies?.Dispose();
			enemies = new EntityComponentFilter<ITargetable>(
				entityCollection,
				(entity) => entity.Identification.HasAny(agent.Relations.Enemies) || agent.Relations.Enemies.Contains(entity.Identification.ID),
				(c) => true,
				agent);
			friends?.Dispose();
			friends = new EntityComponentFilter<ITargetable>(
				entityCollection,
				(entity) => entity.Identification.HasAny(agent.Relations.Friends) || agent.Relations.Friends.Contains(entity.Identification.ID),
				(c) => true,
				agent);
		}

		protected void Awake()
		{
			agent.Relations.RelationsUpdatedEvent += OnRelationsUpdatedEvent;
		}

		protected void OnDestroy()
		{
			enemies.Dispose();
			friends.Dispose();
			agent.Relations.RelationsUpdatedEvent -= OnRelationsUpdatedEvent;
		}

		/// <inheritdoc/>
		public void SetTarget(ITargetable targetable)
		{
			if (targetable != null && !targetable.IsTargetable)
			{
				SpaxDebug.Error("Can't set target", "Target isn't targetable.");
				targetable = null;
			}

			if (Target == targetable)
			{
				return;
			}

			Target = targetable;
			TargetChangedEvent?.Invoke(Target);
		}

		private void OnRelationsUpdatedEvent()
		{
			enemies.Reevaluate();
			friends.Reevaluate();
		}
	}
}
