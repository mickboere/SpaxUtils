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

		[SerializeField] private CombatMove unarmedLight;
		[SerializeField] private CombatMove unarmedHeavy;
		[SerializeField] private CombatMove unarmedBlock;

		private CombatPerformanceHelper performance;
		private IAgent agent;
		private CallbackService callbackService;

		private Dictionary<string, Dictionary<ICombatMove, int>> moves = new Dictionary<string, Dictionary<ICombatMove, int>>();

		public void InjectDependencies(IAgent agent, CallbackService callbackService)
		{
			this.agent = agent;
			this.callbackService = callbackService;
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
				performance = new CombatPerformanceHelper(combatMove, agent, callbackService);
				performance.PerformanceUpdateEvent += OnPerformanceUpdateEvent;
				performance.PerformanceCompletedEvent += OnPerformanceCompletedEvent;
				finalPerformer = performance;
				return true;
			}

			// Move is already being performed, report a negative to allow for input buffering.
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
	}
}
