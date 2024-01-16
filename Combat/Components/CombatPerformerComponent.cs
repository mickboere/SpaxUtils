using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component that is able to execute an <see cref="ICombatMove"/> of which the progression is broadcast through events.
	/// This component does not actually apply anything on an agent-level, for that see <see cref="AgentCombatControllerNode"/>.
	/// </summary>
	public class CombatPerformerComponent : EntityComponentBase, ICombatPerformer
	{
		public event Action<List<HitScanHitData>> NewHitDetectedEvent;
		public event Action<HitData> ProcessHitEvent;
		public event Action<IPerformer> PerformanceUpdateEvent;
		public event Action<IPerformer> PerformanceCompletedEvent;
		public event Action<IPerformer, PoserStruct, float> PoseUpdateEvent;

		public IAct Act => MainPerformance != null ? MainPerformance.Act : null;
		public int Priority => 0;
		public Performance State => MainPerformance != null ? MainPerformance.State : Performance.Inactive;
		public float RunTime => MainPerformance != null ? MainPerformance.RunTime : 0f;

		public ICombatMove CurrentMove => MainPerformance != null ? MainPerformance.CurrentMove : null;
		public float Charge => MainPerformance != null ? MainPerformance.Charge : 0f;

		private CombatPerformanceHelper MainPerformance => helpers.Count > 0 ? helpers[helpers.Count - 1] : null;

		[Header("Default Moves")]
		[SerializeField] private CombatMove unarmedLight;
		[SerializeField] private CombatMove unarmedHeavy;
		[SerializeField] private CombatMove unarmedGuard;
		[Header("Hit Detection")]
		[SerializeField] private LayerMask hitDetectionMask;
		[SerializeField] private float hitPause = 0.15f;
		[SerializeField] private AnimationCurve hitPauseCurve;

		private IAgent agent;
		private CallbackService callbackService;
		private TransformLookup transformLookup;
		private RigidbodyWrapper rigidbodyWrapper;
		private IGrounderComponent grounder;

		private Dictionary<string, Dictionary<ICombatMove, int>> moves = new Dictionary<string, Dictionary<ICombatMove, int>>();
		private List<CombatPerformanceHelper> helpers = new List<CombatPerformanceHelper>();
		private TimedCurveModifier timeMod;

		public void InjectDependencies(IAgent agent, CallbackService callbackService,
			TransformLookup transformLookup, RigidbodyWrapper rigidbodyWrapper, IGrounderComponent grounder)
		{
			this.agent = agent;
			this.callbackService = callbackService;
			this.transformLookup = transformLookup;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.grounder = grounder;
		}

		protected void Start()
		{
			// Add default unarmed moves.
			AddCombatMove(ActorActs.LIGHT, unarmedLight, -1);
			AddCombatMove(ActorActs.HEAVY, unarmedHeavy, -1);
			AddCombatMove(ActorActs.GUARD, unarmedGuard, -1);
		}

		protected void OnDisable()
		{
			foreach (CombatPerformanceHelper helper in helpers)
			{
				helper.Dispose();
			}
			helpers.Clear();
		}

		/// <inheritdoc/>
		public bool SupportsAct(string act)
		{
			return moves.ContainsKey(act);
		}

		/// <inheritdoc/>
		public bool TryPrepare(IAct act, out IPerformer finalPerformer)
		{
			finalPerformer = null;

			// Must be grounded and in control.
			if (!grounder.Grounded || (State == Performance.Inactive && rigidbodyWrapper.Control < 0.5f))
			{
				return false;
			}

			// Must have a supported move.
			ICombatMove combatMove = GetMove(act.Title);
			if (combatMove == null)
			{
				return false;
			}

			// If there isn't already a performance helper running, create a new one and return it.
			// If the existing performance is finishing we can override it because it will dispose of itself once completed.
			if (MainPerformance == null || State == Performance.Finishing || State == Performance.Completed)
			{
				var helper = new CombatPerformanceHelper(act, combatMove, agent, EntityTimeScale, callbackService, transformLookup, hitDetectionMask);
				helper.PerformanceUpdateEvent += OnPerformanceUpdateEvent;
				helper.PerformanceCompletedEvent += OnPerformanceCompletedEvent;
				helper.NewHitDetectedEvent += OnNewHitDetectedEvent;
				helper.PoseUpdateEvent += OnPoseUpdateEvent;
				finalPerformer = helper;
				helpers.Add(helper);
				return true;
			}

			// A move is already being performed, report a negative to allow for input buffering.
			return false;
		}

		/// <inheritdoc/>
		public bool TryPerform()
		{
			if (MainPerformance == null)
			{
				return false;
			}

			return MainPerformance.TryPerform();
		}

		/// <inheritdoc/>
		public bool TryCancel(bool force)
		{
			return MainPerformance == null ? false : MainPerformance.TryCancel(force);
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
			var helper = (CombatPerformanceHelper)performer;
			helpers.Remove(helper);

			performer.PerformanceUpdateEvent -= OnPerformanceUpdateEvent;
			performer.PerformanceCompletedEvent -= OnPerformanceCompletedEvent;

			PerformanceCompletedEvent?.Invoke(performer);

			helper.Dispose();
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
					if (agent.TryGetStat(MainPerformance.CurrentMove.StrengthStat, out EntityStat strengthStat))
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
					if (MainPerformance.CurrentMove.Offensive &&
						agent.TryGetStat(MainPerformance.CurrentMove.OffenceStat, out EntityStat offence) &&
						hittable.Entity.TryGetStat(AgentStatIdentifiers.DEFENCE, out EntityStat defence))
					{
						float damage = SpaxFormulas.GetDamage(offence, defence) * MainPerformance.CurrentMove.Offensiveness;
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
