using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component that is able to execute an <see cref="ICombatMove"/> of which the progression broadcast through events but not applied to the agent.
	/// </summary>
	public class CombatPerformerComponent : EntityComponentBase, ICombatPerformer
	{
		/// <inheritdoc/>
		public event IPerformer.PerformanceUpdateDelegate PerformanceUpdateEvent;
		public event Action<IPerformer> PerformanceCompletedEvent;

		#region Properties

		/// <inheritdoc/>
		public int Priority => 0;

		/// <inheritdoc/>
		public List<string> SupportsActs { get; } = new List<string> { ActorActs.LIGHT, ActorActs.HEAVY };

		/// <inheritdoc/>
		public float PerformanceTime => performance.PerformanceTime;

		/// <inheritdoc/>
		public bool Performing => !(Finishing || Completed);

		/// <inheritdoc/>
		public ICombatMove Current => performance != null ? performance.Current : null;

		/// <inheritdoc/>
		public CombatPerformanceState State => performance.State;

		/// <inheritdoc/>
		public float Charge => performance.Charge;

		#endregion

		#region State getters

		/// <inheritdoc/>
		public bool Charging => performance != null && performance.Charging;

		/// <inheritdoc/>
		public bool Attacking => performance != null && performance.Attacking;

		/// <inheritdoc/>
		public bool Released => performance != null && performance.Released;

		/// <inheritdoc/>
		public bool Finishing => performance != null && performance.Finishing;

		/// <inheritdoc/>
		public bool Completed => performance == null || performance.Completed;

		#endregion

		[SerializeField, Header("Default Moves")] private CombatMove unarmedLight;
		[SerializeField] private CombatMove unarmedHeavy;
		[SerializeField] private CombatMove unarmedBlock;
		[SerializeField, Header("Hit Detection")] private LayerMask hitDetectionMask;
		[SerializeField] private float hitPause = 0.15f;
		[SerializeField] private AnimationCurve hitPauseCurve;

		private IAgent agent;
		private CallbackService callbackService;
		private TransformLookup transformLookup;

		private Dictionary<string, Dictionary<ICombatMove, int>> moves = new Dictionary<string, Dictionary<ICombatMove, int>>();
		private CombatPerformanceHelper performance;
		private TimedCurveModifier timeMod;

		public void InjectDependencies(IAgent agent, CallbackService callbackService, TransformLookup transformLookup)
		{
			this.agent = agent;
			this.callbackService = callbackService;
			this.transformLookup = transformLookup;
		}

		protected void Start()
		{
			// Add default unarmed moves.
			AddCombatMove(ActorActs.LIGHT, unarmedLight, -1);
			AddCombatMove(ActorActs.HEAVY, unarmedHeavy, -1);
			AddCombatMove(ActorActs.BLOCK, unarmedBlock, -1);
		}

		protected void OnDisable()
		{
			performance?.Dispose();
		}

		/// <inheritdoc/>
		public bool TryProduce(IAct act, out IPerformer finalPerformer)
		{
			finalPerformer = performance;
			ICombatMove combatMove = GetMove(act.Title);

			if (combatMove == null)
			{
				return false;
			}

			if (Completed || Finishing)
			{
				performance = new CombatPerformanceHelper(combatMove, agent, EntityTimeScale, callbackService, transformLookup, OnNewHitDetected, hitDetectionMask);
				performance.PerformanceUpdateEvent += OnPerformanceUpdateEvent;
				performance.PerformanceCompletedEvent += OnPerformanceCompletedEvent;
				finalPerformer = performance;
				return true;
			}

			// A move is already being performed, report a negative to allow for input buffering.
			return false;
		}

		/// <inheritdoc/>
		public bool TryPerform()
		{
			return performance.TryPerform();
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
			if (Current != null && Finishing && !Completed)
			{
				foreach (ActCombatPair combo in Current.Combos)
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

		private void OnPerformanceUpdateEvent(IPerformer performer, PoserStruct pose, float weight)
		{
			PerformanceUpdateEvent?.Invoke(performer, pose, weight);
		}

		private void OnPerformanceCompletedEvent(IPerformer performer)
		{
			performer.PerformanceUpdateEvent -= OnPerformanceUpdateEvent;
			performer.PerformanceCompletedEvent -= OnPerformanceCompletedEvent;
			PerformanceCompletedEvent?.Invoke(performer);
		}

		private void OnNewHitDetected(List<HitScanHitData> newHits)
		{
			SpaxDebug.Log($"OnNewHitDetected ({newHits.Count})", $"({string.Join("), (", newHits.Select(n => n.GameObject.name))})");

			if (timeMod != null)
			{
				EntityTimeScale.RemoveModifier(this);
			}

			// TODO: Have hit pause duration depend on factors like weapon attack power, sharpness and hit surface hardness.
			timeMod = new TimedCurveModifier(ModMethod.Absolute, hitPauseCurve, new Timer(hitPause), callbackService);
			EntityTimeScale.AddModifier(this, timeMod);

			foreach (HitScanHitData hit in newHits)
			{
				if (hit.GameObject.TryGetComponentRelative(out IHittable hittable))
				{
					Vector3 momentum = Current.Impact.Momentum.Look((hittable.Entity.Transform.position - Entity.Transform.position).FlattenY());
					float force = 300f; // TODO: Load attack strength from stats.
					HitData hitData = new HitData()
					{
						Hitter = Entity,
						Impact = new ImpactData(hit.Point, momentum, force)
					};
					hittable.Hit(hitData);

					// Apply hit pause to enemy.
					EntityStat hitTimeScale = hittable.Entity.GetStat(StatIdentifierConstants.TIMESCALE);
					if (hitTimeScale != null)
					{
						hitTimeScale.AddModifier(this, timeMod);
					}
				}
			}
		}
	}
}
