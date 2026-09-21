using System;
using System.Linq;
using System.Security.Authentication;
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
				if (_target != null && ((_target is MonoBehaviour mono && !mono) || !_target.IsTargetable))
				{
					// Drops targets that were destroyed or became untargetable, e.g. died.
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
		public ITargetable PreferredTarget
		{
			get
			{
				if (_preferredTarget != null && _preferredTarget is MonoBehaviour mono && !mono)
				{
					_preferredTarget = null;
				}
				return _preferredTarget;
			}
			set { _preferredTarget = value; }
		}
		private ITargetable _preferredTarget;

		/// <inheritdoc/>
		public IEntityComponentFilter<ITargetable> Enemies => enemies;

		/// <inheritdoc/>
		public IEntityComponentFilter<ITargetable> Allies => allies;

		#endregion Properties

		[SerializeField] private bool debug;

		private EntityComponentFilter<ITargetable> enemies;
		private EntityComponentFilter<ITargetable> allies;

		private IAgent agent;
		private TargetingService targetingService;

		public void InjectDependencies(IAgent agent, IEntityCollection entityCollection, TargetingService targetingService)
		{
			this.agent = agent;
			this.targetingService = targetingService;

			enemies?.Dispose();
			enemies = new EntityComponentFilter<ITargetable>(
				entityCollection,
				(other) =>
					agent.Relations.Score(other.Identification) < -AgentRelations.THRESHOLD ||
					(other is IAgent a && a.Relations.Score(agent.Identification) < -AgentRelations.THRESHOLD),
				(c) => true,
				agent);
			allies?.Dispose();
			allies = new EntityComponentFilter<ITargetable>(
				entityCollection,
				(other) =>
					agent.Relations.Score(other.Identification) > AgentRelations.THRESHOLD ||
					(other is IAgent a && a.Relations.Score(agent.Identification) > AgentRelations.THRESHOLD),
				(c) => true,
				agent);
		}

		protected void Awake()
		{
			agent.Relations.RelationsUpdatedEvent += OnRelationsUpdatedEvent;
		}

		protected void OnEnable()
		{
			targetingService?.Register(this);
		}

		protected void OnDisable()
		{
			targetingService?.Unregister(this);
		}

		protected void OnDestroy()
		{
			enemies.Dispose();
			allies.Dispose();
			agent.Relations.RelationsUpdatedEvent -= OnRelationsUpdatedEvent;
		}

		protected void OnDrawGizmos()
		{
			if (debug && Target != null)
			{
				Gizmos.color = Color.red;
				Gizmos.DrawLine(transform.position, Target.Point);
				Gizmos.DrawSphere(Target.Point, 0.1f);
			}
		}

		/// <inheritdoc/>
		public void SetTarget(ITargetable targetable)
		{
			// Untargetable (e.g. just died) is a normal case, not an error: clear instead.
			if (targetable != null && !targetable.IsTargetable)
			{
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
