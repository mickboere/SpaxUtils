using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component that is able to execute an <see cref="IPerformanceMove"/> of which the progression is broadcast through events.
	/// This component does not actually apply anything on an agent-level, for that see <see cref="ActorPerformanceControllerNode"/>.
	/// </summary>
	public class MovePerformerComponent : EntityComponentBase, IMovePerformer
	{
		public event Action<IPerformer> PerformanceUpdateEvent;
		public event Action<IPerformer> PerformanceCompletedEvent;
		public event Action<IPerformer, PoserStruct, float> PoseUpdateEvent;

		public int Priority => 0;
		public IAct Act => MainPerformer != null ? MainPerformer.Act : null;
		public PerformanceState State => MainPerformer != null ? MainPerformer.State : PerformanceState.Inactive;
		public float RunTime => MainPerformer != null ? MainPerformer.RunTime : 0f;

		public IPerformanceMove Move => MainPerformer != null ? MainPerformer.Move : null;
		public float Charge => MainPerformer != null ? MainPerformer.Charge : 0f;

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
			foreach (ActMovePair pair in unarmedMoves)
			{
				AddMove(pair.Act, this, PerformanceState.Inactive | PerformanceState.Finishing | PerformanceState.Completed, pair.Move, pair.Prio);
			}
		}

		protected void OnDisable()
		{
			foreach (MovePerformer helper in helpers)
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
			if (!grounder.Grounded || (State == PerformanceState.Inactive && rigidbodyWrapper.Control <= minimumControl))
			{
				return false;
			}

			// Must have a supported move.
			IPerformanceMove move = GetMove(act.Title);
			if (move == null)
			{
				return false;
			}

			// Stats must exceed costs for move.
			if (move.PerformCost.Count > 0)
			{
				foreach (StatCost statCost in move.PerformCost)
				{
					if (Entity.TryGetStat(statCost.Stat, out EntityStat stat))
					{
						if (stat.Value < statCost.Cost)
						{
							return false;
						}
					}
				}
			}

			var performer = new MovePerformer(dependencyManager, act, move, agent, EntityTimeScale, callbackService);
			performer.PerformanceUpdateEvent += OnPerformanceUpdateEvent;
			performer.PerformanceCompletedEvent += OnPerformanceCompletedEvent;
			performer.PoseUpdateEvent += OnPoseUpdateEvent;
			finalPerformer = performer;
			helpers.Add(performer);
			AddFollowUpMoves(performer, move);
			return true;
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
		}

		/// <summary>
		/// Returns highest prio move for <paramref name="act"/>.
		/// </summary>
		private IPerformanceMove GetMove(string act)
		{
			if (!moves.ContainsKey(act))
			{
				return null;
			}

			(PerformanceState state, IPerformanceMove move, int prio)? top = null;

			foreach (KeyValuePair<object, (PerformanceState state, IPerformanceMove move, int prio)> entry in moves[act])
			{
				if (entry.Value.state.HasFlag(State) && (top == null || entry.Value.prio > top.Value.prio))
				{
					top = entry.Value;
				}
			}

			return top.HasValue ? top.Value.move : null;
		}

		private void AddFollowUpMoves(IPerformer performer, IPerformanceMove move)
		{
			foreach (MoveFollowUp followUp in move.FollowUps)
			{
				AddMove(followUp.Act, performer, followUp.State, followUp.Move, followUp.Prio);
			}
		}

		private void RemoveFollowUpMoves(IPerformer performer)
		{
			string[] acts = moves.Keys.ToArray();
			foreach (string act in acts)
			{
				RemoveMove(act, performer);
			}
		}

		#endregion Move Management

		private void OnPerformanceUpdateEvent(IPerformer performer)
		{
			PerformanceUpdateEvent?.Invoke(performer);
		}

		private void OnPerformanceCompletedEvent(IPerformer performer)
		{
			var helper = (MovePerformer)performer;
			helpers.Remove(helper);

			RemoveFollowUpMoves(performer);

			performer.PerformanceUpdateEvent -= OnPerformanceUpdateEvent;
			performer.PerformanceCompletedEvent -= OnPerformanceCompletedEvent;

			PerformanceCompletedEvent?.Invoke(performer);

			helper.Dispose();
		}

		private void OnPoseUpdateEvent(IPerformer performer, PoserStruct pose, float weight)
		{
			PoseUpdateEvent?.Invoke(performer, pose, weight);
		}
	}
}
