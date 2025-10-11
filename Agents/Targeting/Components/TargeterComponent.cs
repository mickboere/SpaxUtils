using System;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Implementation of <see cref="ITargeter"/>.
	/// Stores an entity's current target as <see cref="ITargetable"/>.
	/// Also keeps track of the agent's enemies and allies as collections of <see cref="IEntityComponentFilter{ITargetable}"/>
	/// </summary>
	public class TargeterComponent : EntityComponentMono, ITargeter
	{
		/// <inheritdoc/>
		public event Action<ITargetable> TargetChangedEvent;

		#region Properties

		/// <inheritdoc/>
		public ITargetable Target
		{
			get
			{
				if (_target != null && _target is MonoBehaviour mono && !mono)
				{
					// Fixes bug where target isn't null even though its destroyed.
					Target = null;
				}
				return _target;
			}
			set
			{
				_target = value;
				TargetChangedEvent?.Invoke(_target);
			}
		}
		private ITargetable _target;

		/// <inheritdoc/>
		public IEntityComponentFilter<ITargetable> Enemies => enemies;

		/// <inheritdoc/>
		public IEntityComponentFilter<ITargetable> Allies => allies;

		#endregion Properties

		private EntityComponentFilter<ITargetable> enemies;
		private EntityComponentFilter<ITargetable> allies;

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
			allies?.Dispose();
			allies = new EntityComponentFilter<ITargetable>(
				entityCollection,
				(entity) => entity.Identification.HasAny(agent.Relations.Allies) || agent.Relations.Allies.Contains(entity.Identification.ID),
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
			allies.Dispose();
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
		}

		private void OnRelationsUpdatedEvent()
		{
			enemies.Reevaluate();
			allies.Reevaluate();
		}
	}
}
