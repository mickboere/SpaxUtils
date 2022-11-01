using System;
using System.Collections.Generic;
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
		public float PerformanceTime => performer.PerformanceTime;

		/// <inheritdoc/>
		public bool Performing => !(Finishing || Completed);

		/// <inheritdoc/>
		public ICombatMove Current => performer != null ? performer.Current : null;

		/// <inheritdoc/>
		public CombatPerformanceState State => performer.State;

		/// <inheritdoc/>
		public float Charge => performer.Charge;

		#region State getters

		/// <inheritdoc/>
		public bool Charging => performer != null && performer.Charging;

		/// <inheritdoc/>
		public bool Attacking => performer != null && performer.Attacking;

		/// <inheritdoc/>
		public bool Released => performer != null && performer.Released;

		/// <inheritdoc/>
		public bool Finishing => performer != null && performer.Finishing;

		/// <inheritdoc/>
		public bool Completed => performer == null || performer.Completed;

		#endregion

		[SerializeField] private CombatMove unarmedLight;
		[SerializeField] private CombatMove unarmedHeavy;

		private CombatPerformanceHelper performer;
		private IAgent agent;
		private CallbackService callbackService;

		public void InjectDependencies(IAgent agent, CallbackService callbackService)
		{
			this.agent = agent;
			this.callbackService = callbackService;
		}

		protected void OnDisable()
		{
			performer?.Dispose();
		}

		/// <inheritdoc/>
		public bool TryProduce(IAct act, out IPerformer finalPerformer)
		{
			finalPerformer = performer;
			CombatMove combatMove = null;
			if (act.Title == ActorActs.LIGHT)
			{
				combatMove = unarmedLight;
			}
			if (act.Title == ActorActs.HEAVY)
			{
				combatMove = unarmedHeavy;
			}

			if (combatMove == null)
			{
				return false;
			}

			if (Completed || Finishing)
			{
				performer = new CombatPerformanceHelper(combatMove, agent, callbackService);
				performer.PerformanceUpdateEvent += OnPerformanceUpdateEvent;
				performer.PerformanceCompletedEvent += OnPerformanceCompletedEvent;
				finalPerformer = performer;
				return true;
			}

			// Move is already being performed, report a negative to allow for input buffering.
			return false;
		}

		/// <inheritdoc/>
		public bool TryPerform()
		{
			return performer.TryPerform();
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
