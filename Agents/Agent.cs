using SpaxUtils.StateMachines;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Base <see cref="IAgent"/> implementation.
	/// </summary>
	public class Agent : Entity, IAgent, IDependencyProvider
	{
		/// <inheritdoc/>
		public IActor Actor { get; } = new Actor();

		/// <inheritdoc/>
		public IBrain Brain { get; private set; }

		/// <inheritdoc/>
		public IAgentBody Body { get; private set; }

		/// <inheritdoc/>
		public ITargetable Targetable { get; private set; }

		/// <inheritdoc/>
		public ITargeter Targeter { get; private set; }

		protected override string GameObjectNamePrefix => "[Agent]";

		[SerializeField, ConstDropdown(typeof(IStateIdentifierConstants))] private string state;
		[SerializeField] private List<StateMachineGraph> brainGraphs;

		private CallbackService callbackService;

		/// <inheritdoc/>
		public Dictionary<object, object> RetrieveDependencies()
		{
			var dependencies = new Dictionary<object, object>();
			dependencies.Add(typeof(Actor), Actor);
			return dependencies;
		}

		public void InjectDependencies(
			IAgentBody body, ITargetable targetableComponent, ITargeter targeterComponent,
			IPerformer[] performers, CallbackService callbackService)
		{
			Body = body;
			Targetable = targetableComponent;
			Targeter = targeterComponent;
			this.callbackService = callbackService;

			foreach (IPerformer performer in performers)
			{
				if (performer != Actor)
				{
					Actor.AddPerformer(performer);
				}
			}
		}

		protected override void Awake()
		{
			base.Awake();

			// Initialize brain.
			Brain = new Brain(DependencyManager, callbackService, state, null, brainGraphs);
			Brain.EnteredStateEvent += OnEnteredStateEvent;
			Brain.Start();
		}

		protected void OnDestroy()
		{
			((Actor)Actor).Dispose();
			Brain.Dispose();
		}

		/// <summary>
		/// Will store <paramref name="graphs"/> during initialization to inject them during the Brain's creation.
		/// </summary>
		/// <param name="graphs">The <see cref="StateMachineGraph"/>(s) to initialize the brain with.</param>
		public void AddInitialBrainGraphs(IEnumerable<StateMachineGraph> graphs)
		{
			if (Brain != null)
			{
				SpaxDebug.Error("Brain graphs cannot be added to Agent because the Brain has already been initialized.", "Use Brain.AppendGraph() instead.");
				return;
			}

			foreach (StateMachineGraph graph in graphs)
			{
				if (!brainGraphs.Contains(graph))
				{
					brainGraphs.Add(graph);
				}
			}
		}

		private void OnEnteredStateEvent(IState state)
		{
			this.state = state.ID;

			SpaxDebug.Notify($"[{Identification.Name}]", $"OnEnteredStateEvent({string.Join(", ", Brain.StateHierarchy.Select(s => s.ID))})");
		}
	}
}
