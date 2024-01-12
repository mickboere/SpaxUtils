using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component that is able to execute an <see cref="ICombatMove"/> of which the progression is broadcast through events.
	/// This component does not actually change anything on an agent-level, for that see <see cref="AgentCombatControllerNode"/>.
	/// </summary>
	public class CombatPerformerComponent : EntityComponentBase, ICombatPerformer
	{
		/// <inheritdoc/>
		public event Action<List<HitScanHitData>> NewHitDetectedEvent;

		/// <inheritdoc/>
		public event Action<HitData> ProcessHitEvent;

		/// <inheritdoc/>
		public event Action<IPerformer> PerformanceUpdateEvent;

		/// <inheritdoc/>
		public event Action<IPerformer> PerformanceCompletedEvent;

		/// <inheritdoc/>
		public event Action<IPerformer, PoserStruct, float> PoseUpdateEvent;

		#region IPerformer Properties

		/// <inheritdoc/>
		public int Priority => 0;

		/// <inheritdoc/>
		public List<string> SupportsActs { get; } = new List<string> { ActorActs.LIGHT, ActorActs.HEAVY };

		/// <inheritdoc/>
		public Performance State => performanceHelper != null ? performanceHelper.State : Performance.Inactive;

		/// <inheritdoc/>
		public float RunTime => performanceHelper != null ? performanceHelper.RunTime : 0f;

		#endregion IPerformer Properties

		#region ICombatPerformer Properties

		/// <inheritdoc/>
		public ICombatMove CurrentMove => performanceHelper != null ? performanceHelper.CurrentMove : null;

		/// <inheritdoc/>
		public float Charge => performanceHelper != null ? performanceHelper.Charge : 0f;

		#endregion ICombatPerformer Properties

		[SerializeField, Header("Default Moves")] private CombatMove unarmedLight;
		[SerializeField] private CombatMove unarmedHeavy;
		[SerializeField] private CombatMove unarmedBlock;
		[SerializeField, Header("Hit Detection")] private LayerMask hitDetectionMask;
		[SerializeField] private float hitPause = 0.15f;
		[SerializeField] private AnimationCurve hitPauseCurve;

		private IAgent agent;
		private CallbackService callbackService;
		private TransformLookup transformLookup;
		private RigidbodyWrapper rigidbodyWrapper;

		private Dictionary<string, Dictionary<ICombatMove, int>> moves = new Dictionary<string, Dictionary<ICombatMove, int>>();
		private CombatPerformanceHelper performanceHelper;
		private TimedCurveModifier timeMod;

		public void InjectDependencies(IAgent agent, CallbackService callbackService,
			TransformLookup transformLookup, RigidbodyWrapper rigidbodyWrapper)
		{
			this.agent = agent;
			this.callbackService = callbackService;
			this.transformLookup = transformLookup;
			this.rigidbodyWrapper = rigidbodyWrapper;
		}

		protected void Start()
		{
			// Add default unarmed moves.
			AddCombatMove(ActorActs.LIGHT, unarmedLight, -1);
			AddCombatMove(ActorActs.HEAVY, unarmedHeavy, -1);
		}

		protected void OnDisable()
		{
			performanceHelper?.Dispose();
		}

		/// <inheritdoc/>
		public bool TryPrepare(IAct act, out IPerformer finalPerformer)
		{
			finalPerformer = performanceHelper;
			ICombatMove combatMove = GetMove(act.Title);

			if (combatMove == null)
			{
				return false;
			}

			if (performanceHelper == null || State == Performance.Finishing)
			{
				performanceHelper = new CombatPerformanceHelper(combatMove, agent, EntityTimeScale, callbackService, transformLookup, hitDetectionMask);
				performanceHelper.PerformanceUpdateEvent += OnPerformanceUpdateEvent;
				performanceHelper.PerformanceCompletedEvent += OnPerformanceCompletedEvent;
				performanceHelper.NewHitDetectedEvent += OnNewHitDetectedEvent;
				performanceHelper.PoseUpdateEvent += OnPoseUpdateEvent;
				finalPerformer = performanceHelper;
				return true;
			}

			// A move is already being performed, report a negative to allow for input buffering.
			return false;
		}

		/// <inheritdoc/>
		public bool TryPerform()
		{
			if (performanceHelper == null)
			{
				return false;
			}

			return performanceHelper.TryPerform();
		}

		public bool TryCancel()
		{
			if (performanceHelper == null)
			{
				return false;
			}

			return performanceHelper.TryCancel();
		}

		/// <inheritdoc/>
		public void AddCombatMove(string act, ICombatMove move, int prio)
		{
			if (move == null)
			{
				return;
			}

			// Ensure act.
			if (!moves.ContainsKey(act))
			{
				moves.Add(act, new Dictionary<ICombatMove, int>());
			}

			// Set move prio.
			moves[act][move] = prio;
		}

		/// <inheritdoc/>
		public void RemoveCombatMove(string act, ICombatMove move)
		{
			if (moves.ContainsKey(act) && moves[act].ContainsKey(move))
			{
				moves[act].Remove(move);
			}
		}

		/// <summary>
		/// Returns highest prio move for <paramref name="act"/>.
		/// </summary>
		private ICombatMove GetMove(string act)
		{
			// Check for possible combo / follow up move.
			if (CurrentMove != null && State == Performance.Finishing)
			{
				foreach (ActCombatPair combo in CurrentMove.FollowUps)
				{
					if (combo.Act == act)
					{
						return combo.Move;
					}
				}
			}

			if (moves.ContainsKey(act))
			{
				KeyValuePair<ICombatMove, int> top = moves[act].FirstOrDefault();
				foreach (KeyValuePair<ICombatMove, int> kvp in moves[act])
				{
					if (kvp.Value > top.Value)
					{
						top = kvp;
					}
				}

				return top.Key;
			}

			return null;
		}

		private void OnPerformanceUpdateEvent(IPerformer performer)
		{
			PerformanceUpdateEvent?.Invoke(performer);
		}

		private void OnPerformanceCompletedEvent(IPerformer performer)
		{
			performer.PerformanceUpdateEvent -= OnPerformanceUpdateEvent;
			performer.PerformanceCompletedEvent -= OnPerformanceCompletedEvent;
			PerformanceCompletedEvent?.Invoke(performer);
		}

		private void OnPoseUpdateEvent(IPerformer performer, PoserStruct pose, float weight)
		{
			PoseUpdateEvent?.Invoke(performer, pose, weight);
		}

		private void OnNewHitDetectedEvent(List<HitScanHitData> newHits)
		{
			// TODO: Have hit pause duration depend on penetration % (factoring in power, sharpness, hardness.)
			timeMod = new TimedCurveModifier(ModMethod.Absolute, hitPauseCurve, new Timer(hitPause), callbackService);

			bool successfulHit = false;
			foreach (HitScanHitData hit in newHits)
			{
				if (hit.GameObject.TryGetComponentRelative(out IHittable hittable))
				{
					Vector3 inertia = CurrentMove.Inertia.Look((hittable.Entity.Transform.position - Entity.Transform.position).FlattenY());

					// Calculate attack force.
					float strength = 0f;
					if (agent.TryGetStat(performanceHelper.CurrentMove.StrengthStat, out EntityStat strengthStat))
					{
						strength = strengthStat;
					}

					float force = rigidbodyWrapper.Mass + strength;

					// Generate hit-data for hittable.
					HitData hitData = new HitData(
						Entity,
						hittable,
						inertia,
						force,
						hit.Direction,
						new Dictionary<string, float>()
					);

					// If move is offensive, add base health damage to HitData.
					if (performanceHelper.CurrentMove.Offensive &&
						agent.TryGetStat(performanceHelper.CurrentMove.OffenceStat, out EntityStat offence) &&
						hittable.Entity.TryGetStat(AgentStatIdentifiers.DEFENCE, out EntityStat defence))
					{
						float damage = SpaxFormulas.GetDamage(offence, defence) * performanceHelper.CurrentMove.Offensiveness;
						hitData.Damages.Add(AgentStatIdentifiers.HEALTH, damage);
					}

					// Invoke hit event to allow adding of additional damage.
					ProcessHitEvent?.Invoke(hitData);

					if (hittable.Hit(hitData))
					{
						successfulHit = true;

						// Apply hit pause to enemy.
						// TODO: Must be applied on enemy's end.
						EntityStat hitTimeScale = hittable.Entity.GetStat(EntityStatIdentifier.TIMESCALE);
						if (hitTimeScale != null)
						{
							hitTimeScale.AddModifier(this, timeMod);
						}
					}
				}
			}

			if (successfulHit)
			{
				EntityTimeScale.RemoveModifier(this);
				EntityTimeScale.AddModifier(this, timeMod);
			}
		}
	}
}
