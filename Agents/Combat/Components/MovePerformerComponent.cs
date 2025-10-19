using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component that is able to execute an <see cref="IPerformanceMove"/> of which the progression is broadcast through events.
	/// This component does not actually apply anything on an agent-level, for that see <see cref="AgentPerformanceControllerNode"/>.
	/// </summary>
	public class MovePerformerComponent : EntityComponentMono, IMovePerformanceHandler
	{
		public event Action<IPerformer> PerformanceStartedEvent;
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
		public IPerformanceMove Move => MainPerformer != null ? MainPerformer.Move : null;
		/// <inheritdoc/>
		public float Charge => MainPerformer != null ? MainPerformer.Charge : 0f;
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
			foreach (ActMovePair pair in unarmedMoves)
			{
				AddMove(pair.Act, pair.Move, PerformanceState.Inactive | PerformanceState.Finishing | PerformanceState.Completed, pair.Prio);
			}
			autoUpdateMoveset = true;
			UpdateMoveset();
		}

		protected void OnDestroy()
		{
			foreach (MovePerformer helper in helpers)
			{
				helper.Dispose();
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
			foreach (BehaviourAsset behaviour in move.Behaviour)
			{
				if (behaviour is IPrerequisite prerequisite && !prerequisite.IsMet(dependencyManager))
				{
					return false;
				}
			}

			// 4. Utilized stats must exceed 0.
			// Note: (most) stats don't have to exceed costs since they will overdraw from the "recoverable" stat.
			if ((move.HasCharge && !ValidateStat(move.ChargeCost)) || (move.HasPerformance && !ValidateStat(move.PerformCost)))
			{
				return false;
			}

			var performer = new MovePerformer(dependencyManager, act, move, agent, EntityTimeScale, callbackService);
			performer.PerformanceStartedEvent += OnPerformanceStartedEvent;
			performer.PerformanceUpdateEvent += OnPerformanceUpdateEvent;
			performer.PerformanceCompletedEvent += OnPerformanceCompletedEvent;
			finalPerformer = performer;
			helpers.Add(performer);
			return true;

			bool ValidateStat(StatCost cost)
			{
				// Will validate whether a cost stat is defined, and if it is, whether it is greater than 0.
				// If the cost stat is not defined it is also valid, since no cost will need to be substracted.
				return !cost.Required || string.IsNullOrEmpty(cost.Stat) || (Entity.Stats.TryGetStat(cost.Stat, out EntityStat stat) && stat.Value > 0f);
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
				// Update the active moveset.
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
			foreach (MoveFollowUp followUp in move.FollowUps)
			{
				AddMove(followUp.Act, followUp.Move, followUp.State, followUp.Prio, move);
			}
			autoUpdateMoveset = true;
		}

		private void RemoveFollowUpMoves(IPerformanceMove move)
		{
			autoUpdateMoveset = false;
			foreach (MoveFollowUp followUp in move.FollowUps)
			{
				RemoveMove(followUp.Act, followUp.Move);
			}
			autoUpdateMoveset = true;
		}

		private void UpdateMoveset()
		{
			// Collect all the currently available highest-priority moves per act.
			// Must be updated with each change in either performance state or available moves.
			moveset = new Dictionary<string, IPerformanceMove>();
			foreach (string act in moves.Keys)
			{
				KeyValuePair<IPerformanceMove, (PerformanceState state, int prio, IPerformanceMove prior)>? top = null;
				foreach (KeyValuePair<IPerformanceMove, (PerformanceState state, int prio, IPerformanceMove prior)> entry in moves[act])
				{
					if (entry.Value.state.HasFlag(State) &&
						(entry.Value.prior == null || entry.Value.prior == Move) &&
						(top == null || entry.Value.prio > top.Value.Value.prio ||
							(entry.Value.prio == top.Value.Value.prio && top.Value.Value.prior == null && entry.Value.prior != null)))
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

		#endregion Move Management

		private void OnPerformanceStartedEvent(IPerformer performer)
		{
			PerformanceStartedEvent?.Invoke(performer);
		}

		private void OnPerformanceUpdateEvent(IPerformer performer)
		{
			if (State != lastState)
			{
				lastState = State;
				UpdateMoveset();
			}
			PerformanceUpdateEvent?.Invoke(performer);
		}

		private void OnPerformanceCompletedEvent(IPerformer performer)
		{
			var movePerformer = (MovePerformer)performer;
			helpers.Remove(movePerformer);

			performer.PerformanceStartedEvent -= OnPerformanceStartedEvent;
			performer.PerformanceUpdateEvent -= OnPerformanceUpdateEvent;
			performer.PerformanceCompletedEvent -= OnPerformanceCompletedEvent;

			PerformanceCompletedEvent?.Invoke(performer);

			movePerformer.Dispose();
		}
	}
}
