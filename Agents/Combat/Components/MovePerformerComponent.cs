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
	public class MovePerformerComponent : EntityComponentBase, IMovePerformanceHandler
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
		private IGrounderComponent grounder;
		private RigidbodyWrapper rigidbodyWrapper;
		private CallbackService callbackService;

		private Dictionary<string, Dictionary<object, (PerformanceState state, IPerformanceMove move, int prio)>> moves =
			new Dictionary<string, Dictionary<object, (PerformanceState state, IPerformanceMove move, int prio)>>();
		private Dictionary<string, IPerformanceMove> moveset;
		private bool updateMoveset = true;
		private PerformanceState lastState = PerformanceState.Inactive;
		private List<MovePerformer> helpers = new List<MovePerformer>();

		public void InjectDependencies(IDependencyManager dependencyManager, IAgent agent,
			IGrounderComponent grounder, RigidbodyWrapper rigidbodyWrapper, CallbackService callbackService)
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
			updateMoveset = false;
			foreach (ActMovePair pair in unarmedMoves)
			{
				AddMove(pair.Act, this, PerformanceState.Inactive | PerformanceState.Finishing | PerformanceState.Completed, pair.Move, pair.Prio);
			}
			updateMoveset = true;
			UpdateMoveset();
		}

		protected void OnDisable()
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

			// Must be grounded and in control.
			if (!grounder.Grounded || (State == PerformanceState.Inactive && rigidbodyWrapper.Control <= minimumControl))
			{
				return false;
			}

			// Must have a supported move.
			if (!Moveset.ContainsKey(act.Title))
			{
				return false;
			}
			IPerformanceMove move = Moveset[act.Title];

			// Utilized stats must exceed 0.
			// Note: stats don't have to exceed costs since they will overdraw from the "recoverable" stat.
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
			AddFollowUpMoves(performer, move);
			return true;

			bool ValidateStat(StatCost cost)
			{
				if (!string.IsNullOrEmpty(cost.Stat) && Entity.TryGetStat(cost.Stat, out EntityStat stat))
				{
					if (stat.Value <= Mathf.Epsilon)
					{
						return false;
					}
				}
				return true;
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
		public void AddMove(string act, object context, PerformanceState state, IPerformanceMove move, int prio)
		{
			if (move == null)
			{
				return;
			}

			// Ensure act.
			if (!moves.ContainsKey(act))
			{
				moves.Add(act, new Dictionary<object, (PerformanceState state, IPerformanceMove move, int prio)>());
			}

			// Set move prio.
			moves[act][context] = (state, move, prio);

			if (updateMoveset)
			{
				UpdateMoveset();
			}
		}

		/// <inheritdoc/>
		public void RemoveMove(string act, object context)
		{
			if (moves.ContainsKey(act) && moves[act].ContainsKey(context))
			{
				moves[act].Remove(context);
			}
			if (moves[act].Count == 0)
			{
				moves.Remove(act);
			}

			if (updateMoveset)
			{
				UpdateMoveset();
			}
		}

		private void AddFollowUpMoves(IPerformer performer, IPerformanceMove move)
		{
			updateMoveset = false;
			foreach (MoveFollowUp followUp in move.FollowUps)
			{
				AddMove(followUp.Act, performer, followUp.State, followUp.Move, followUp.Prio);
			}
			updateMoveset = true;
			UpdateMoveset();
		}

		private void RemoveFollowUpMoves(IPerformer performer)
		{
			updateMoveset = false;
			string[] acts = moves.Keys.ToArray();
			foreach (string act in acts)
			{
				RemoveMove(act, performer);
			}
			updateMoveset = true;
			UpdateMoveset();
		}

		private void UpdateMoveset()
		{
			// Collect all the currently available highest-priority moves.
			// Must be updated with each change in either performance state or available moves.
			moveset = new Dictionary<string, IPerformanceMove>();
			foreach (string act in moves.Keys)
			{
				(PerformanceState state, IPerformanceMove move, int prio)? top = null;
				foreach (KeyValuePair<object, (PerformanceState state, IPerformanceMove move, int prio)> entry in moves[act])
				{
					if (entry.Value.state.HasFlag(State) && (top == null || entry.Value.prio > top.Value.prio))
					{
						top = entry.Value;
					}
				}
				if (top.HasValue)
				{
					moveset.Add(act, top.Value.move);
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
			var helper = (MovePerformer)performer;
			helpers.Remove(helper);

			RemoveFollowUpMoves(performer);

			performer.PerformanceStartedEvent -= OnPerformanceStartedEvent;
			performer.PerformanceUpdateEvent -= OnPerformanceUpdateEvent;
			performer.PerformanceCompletedEvent -= OnPerformanceCompletedEvent;

			PerformanceCompletedEvent?.Invoke(performer);

			helper.Dispose();
		}
	}
}
