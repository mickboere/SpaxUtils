using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component that is able to execute an <see cref="IPerformanceMove"/> of which the progression is broadcast through events.
	/// This component does not actually apply anything on an agent-level, for that see <see cref="AgentPerformanceControllerNode"/>.
	/// </summary>
	public class MovePerformerComponent : EntityComponentMono, IMovePerformanceHandler
	{
		public event Action<IPerformer> StartedPreparingEvent;
		public event Action<IPerformer> StartedPerformingEvent;
		public event Action<IPerformer> PerformanceUpdateEvent;
		public event Action<IPerformer> PerformanceCompletedEvent;
		public event Action MovesetUpdatedEvent;

		/// <inheritdoc/>
		public int Priority => 0;
		/// <inheritdoc/>
		public IAct Act => MainPerformer != null ? MainPerformer.Act : null;
		/// <inheritdoc/>
		public PerformanceState State => MainPerformer != null ? MainPerformer.State : PerformanceState.Inactive;
		/// <inheritdoc/>
		public float RunTime => MainPerformer != null ? MainPerformer.RunTime : 0f;
		/// <inheritdoc/>
		public float Weight => MainPerformer != null ? MainPerformer.Weight : 0f;

		/// <inheritdoc/>
		public IPerformanceMove Move => MainPerformer != null ? MainPerformer.Move : null;
		/// <inheritdoc/>
		public float PrepTime => MainPerformer != null ? MainPerformer.PrepTime : 0f;
		/// <inheritdoc/>
		public bool Prolong
		{
			get { return MainPerformer != null ? MainPerformer.Prolong : false; }
			set { if (MainPerformer != null) MainPerformer.Prolong = value; }
		}

		/// <inheritdoc/>
		public bool Paused
		{
			get { return MainPerformer != null ? MainPerformer.Paused : false; }
			set { if (MainPerformer != null) MainPerformer.Paused = value; }
		}
		/// <inheritdoc/>
		public bool Canceled => MainPerformer != null ? MainPerformer.Canceled : false;
		/// <inheritdoc/>
		public float CancelTime => MainPerformer != null ? MainPerformer.CancelTime : 0f;

		/// <inheritdoc/>
		public IReadOnlyDictionary<string, Dictionary<IPerformanceMove, (PerformanceState state, int prio, IPerformanceMove prior)>> Moves => moves;

		/// <inheritdoc/>
		public IReadOnlyDictionary<string, IPerformanceMove> Moveset => moveset;

		private MovePerformer MainPerformer => helpers.Count > 0 ? helpers[helpers.Count - 1] : null;

		[SerializeField] private List<ActMovePair> unarmedMoves;
		[SerializeField, Range(0f, 1f)] private float minimumControl = 0.7f;

		private IDependencyManager dependencyManager;
		private IAgent agent;
		private GrounderComponent grounder;
		private RigidbodyWrapper rigidbodyWrapper;
		private CallbackService callbackService;

		private Dictionary<string, Dictionary<IPerformanceMove, (PerformanceState state, int prio, IPerformanceMove prior)>> moves =
			new Dictionary<string, Dictionary<IPerformanceMove, (PerformanceState state, int prio, IPerformanceMove prior)>>();
		private Dictionary<string, IPerformanceMove> moveset;
		private bool autoUpdateMoveset = true;

		private PerformanceState lastState = PerformanceState.Inactive;
		private bool lastCanceled;

		private List<MovePerformer> helpers = new List<MovePerformer>();
		private List<IPerformanceMove> processing = new List<IPerformanceMove>(); // Used to prevent stack overflows.

		public void InjectDependencies(IDependencyManager dependencyManager, IAgent agent,
			GrounderComponent grounder, RigidbodyWrapper rigidbodyWrapper, CallbackService callbackService)
		{
			this.dependencyManager = dependencyManager;
			this.agent = agent;
			this.grounder = grounder;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.callbackService = callbackService;
		}

		protected void Start()
		{
			// Add default unarmed moves.
			autoUpdateMoveset = false;
			for (int i = 0; i < unarmedMoves.Count; i++)
			{
				ActMovePair pair = unarmedMoves[i];
				AddMove(pair.Act, pair.Move,
					PerformanceState.Inactive | PerformanceState.Finishing | PerformanceState.Completed,
					pair.Prio);
			}
			autoUpdateMoveset = true;
			UpdateMoveset();
		}

		protected void OnDestroy()
		{
			for (int i = 0; i < helpers.Count; i++)
			{
				helpers[i].Dispose();
			}
			helpers.Clear();
		}

		#region Performance

		/// <inheritdoc/>
		public bool SupportsAct(string act)
		{
			return moves.ContainsKey(act);
		}

		/// <inheritdoc/>
		public bool TryPrepare(IAct act, out IPerformer finalPerformer)
		{
			finalPerformer = null;

			// 1. Must be grounded and in control (default prerequisite for all moves, may be changed in future).
			if (!grounder.Grounded || (State == PerformanceState.Inactive && rigidbodyWrapper.Control <= minimumControl))
			{
				return false;
			}

			// 2. Must have a supported move.
			if (!Moveset.ContainsKey(act.Title))
			{
				return false;
			}
			IPerformanceMove move = Moveset[act.Title];

			// 3. All behavioral prerequisites must be met.
			for (int i = 0; i < move.Behaviour.Count; i++)
			{
				BehaviourAsset behaviour = move.Behaviour[i];
				if (behaviour is IPrerequisite prerequisite && !prerequisite.IsMet(dependencyManager))
				{
					return false;
				}
			}

			// 4. Utilized stats must exceed 0.
			// Note: (most) stats don't have to exceed costs since they will overdraw from the "recoverable" stat.
			if ((move.HasPrep && !ValidateStat(move.PrepCost)) || (move.HasPerformance && !ValidateStat(move.PerformCost)))
			{
				return false;
			}

			MovePerformer performer = new MovePerformer(dependencyManager, act, move, agent, EntityTimeScale, callbackService);
			performer.StartedPerformingEvent += OnPerformanceStartedEvent;
			performer.PerformanceUpdateEvent += OnPerformanceUpdateEvent;
			performer.PerformanceCompletedEvent += OnPerformanceCompletedEvent;

			finalPerformer = performer;
			helpers.Add(performer);
			StartedPreparingEvent?.Invoke(performer);
			return true;

			bool ValidateStat(StatCost cost)
			{
				// Will validate whether a cost stat is defined, and if it is, whether it is greater than 0.
				// If the cost stat is not defined it is also valid, since no cost will need to be subtracted.
				if (!cost.Required || string.IsNullOrEmpty(cost.Stat))
				{
					return true;
				}

				if (!Entity.Stats.TryGetStat(cost.Stat, out EntityStat stat))
				{
					return false;
				}

				return stat.Value > 0f;
			}
		}

		/// <inheritdoc/>
		public bool TryPerform()
		{
			if (MainPerformer == null)
			{
				return false;
			}

			return MainPerformer.TryPerform();
		}

		/// <inheritdoc/>
		public bool TryCancel(bool force)
		{
			return MainPerformer == null ? false : MainPerformer.TryCancel(force);
		}

		#endregion Performance

		#region Move Management

		/// <inheritdoc/>
		public void AddMove(string act, IPerformanceMove move, PerformanceState state, int prio, IPerformanceMove prior = null)
		{
			if (move == null || processing.Contains(move))
			{
				return;
			}

			processing.Add(move);

			// Ensure act.
			if (!moves.ContainsKey(act))
			{
				moves.Add(act, new Dictionary<IPerformanceMove, (PerformanceState state, int prio, IPerformanceMove prior)>());
			}

			// Store move.
			moves[act][move] = (state, prio, prior);

			// Store follow-up moves for this move.
			AddFollowUpMoves(move);

			if (autoUpdateMoveset)
			{
				UpdateMoveset();
			}

			processing.Remove(move);
		}

		/// <inheritdoc/>
		public void RemoveMove(string act, IPerformanceMove move)
		{
			if (move == null || processing.Contains(move))
			{
				return;
			}

			processing.Add(move);

			if (moves.ContainsKey(act))
			{
				// Remove move from storage.
				if (moves[act].ContainsKey(move))
				{
					moves[act].Remove(move);
				}
				// If no moves remain for said act, remove act.
				if (moves[act].Count == 0)
				{
					moves.Remove(act);
				}
				// Also remove all follow-up moves for this move.
				RemoveFollowUpMoves(move);
			}

			if (autoUpdateMoveset)
			{
				UpdateMoveset();
			}

			processing.Remove(move);
		}

		private void AddFollowUpMoves(IPerformanceMove move)
		{
			autoUpdateMoveset = false;
			for (int i = 0; i < move.FollowUps.Count; i++)
			{
				MoveFollowUp followUp = move.FollowUps[i];
				AddMove(followUp.Act, followUp.Move, followUp.State, followUp.Prio, move);
			}
			autoUpdateMoveset = true;
		}

		private void RemoveFollowUpMoves(IPerformanceMove move)
		{
			autoUpdateMoveset = false;
			for (int i = 0; i < move.FollowUps.Count; i++)
			{
				MoveFollowUp followUp = move.FollowUps[i];
				RemoveMove(followUp.Act, followUp.Move);
			}
			autoUpdateMoveset = true;
		}

		private void UpdateMoveset()
		{
			// Collect all the currently available highest-priority moves per act.
			// Must be updated with each change in either performance state or available moves.
			moveset = new Dictionary<string, IPerformanceMove>();

			PerformanceState state = State;
			IPerformanceMove currentMove = Move;

			foreach (string act in moves.Keys)
			{
				KeyValuePair<IPerformanceMove, (PerformanceState state, int prio, IPerformanceMove prior)>? top = null;
				foreach (KeyValuePair<IPerformanceMove, (PerformanceState state, int prio, IPerformanceMove prior)> entry in moves[act])
				{
					var meta = entry.Value;

					if (!meta.state.HasFlag(state))
					{
						continue;
					}

					if (meta.prior != null && meta.prior != currentMove)
					{
						continue;
					}

					if (top == null ||
						meta.prio > top.Value.Value.prio ||
						(meta.prio == top.Value.Value.prio && top.Value.Value.prior == null && meta.prior != null))
					{
						top = entry;
					}
				}
				if (top.HasValue)
				{
					moveset.Add(act, top.Value.Key);
				}
			}

			MovesetUpdatedEvent?.Invoke();
		}

		// Expose all reachable combat moves and their input chains (including combo finishers).
		public IEnumerable<(ICombatMove move, string[] actChain)> EnumerateCombatChains(int maxDepth = 3)
		{
			if (moves == null || moves.Count == 0)
			{
				yield break;
			}

			var visited = new HashSet<IPerformanceMove>();

			foreach (var actEntry in moves)
			{
				string act = actEntry.Key;
				var moveDict = actEntry.Value;

				foreach (var moveEntry in moveDict)
				{
					IPerformanceMove move = moveEntry.Key;
					var meta = moveEntry.Value;

					if (meta.prior != null)
					{
						continue; // not a root
					}

					var chain = new List<string> { act };
					foreach (var combo in EnumerateFrom(move, chain, visited, maxDepth, 1))
					{
						yield return combo;
					}
				}
			}
		}

		private IEnumerable<(ICombatMove move, string[] actChain)> EnumerateFrom(
			IPerformanceMove move,
			List<string> chain,
			HashSet<IPerformanceMove> visited,
			int maxDepth,
			int depth)
		{
			if (move == null)
			{
				yield break;
			}

			if (visited.Contains(move))
			{
				yield break;
			}
			visited.Add(move);

			if (move is ICombatMove combatMove)
			{
				yield return (combatMove, chain.ToArray());
			}

			if (depth >= maxDepth)
			{
				visited.Remove(move);
				yield break;
			}

			foreach (var actEntry in moves)
			{
				string act = actEntry.Key;
				var moveDict = actEntry.Value;

				foreach (var moveEntry in moveDict)
				{
					IPerformanceMove childMove = moveEntry.Key;
					var meta = moveEntry.Value;

					if (meta.prior != move)
					{
						continue;
					}

					chain.Add(act);
					foreach (var combo in EnumerateFrom(childMove, chain, visited, maxDepth, depth + 1))
					{
						yield return combo;
					}
					chain.RemoveAt(chain.Count - 1);
				}
			}

			visited.Remove(move);
		}

		#endregion Move Management

		private void OnPerformanceStartedEvent(IPerformer performer)
		{
			StartedPerformingEvent?.Invoke(performer);
		}

		private void OnPerformanceUpdateEvent(IPerformer performer)
		{
			// Recompute moveset whenever the *top* performer's state or cancel-flag effectively changes,
			// so follow-ups see the correct (Move, State) combo, and cancel transitions also update.
			PerformanceState state = State;
			bool canceled = Canceled;

			if (state != lastState || canceled != lastCanceled)
			{
				lastState = state;
				lastCanceled = canceled;
				UpdateMoveset();
			}

			PerformanceUpdateEvent?.Invoke(performer);
		}

		private void OnPerformanceCompletedEvent(IPerformer performer)
		{
			var movePerformer = (MovePerformer)performer;
			helpers.Remove(movePerformer);

			performer.StartedPerformingEvent -= OnPerformanceStartedEvent;
			performer.PerformanceUpdateEvent -= OnPerformanceUpdateEvent;
			performer.PerformanceCompletedEvent -= OnPerformanceCompletedEvent;

			PerformanceCompletedEvent?.Invoke(performer);

			movePerformer.Dispose();

			// After removing the performer, the main performer may have changed,
			// so update cached state and moveset once.
			PerformanceState state = State;
			bool canceled = Canceled;
			if (state != lastState || canceled != lastCanceled)
			{
				lastState = state;
				lastCanceled = canceled;
				UpdateMoveset();
			}
		}
	}
}
