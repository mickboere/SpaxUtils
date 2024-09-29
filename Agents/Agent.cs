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
	public class Agent : Entity, IAgent
	{
		public event Action<IAgent> DiedEvent;
		public event Action<IAgent> RevivedEvent;

		#region Properties

		/// <inheritdoc/>
		public IActor Actor { get; private set; }

		/// <inheritdoc/>
		public IBrain Brain { get; private set; }

		/// <inheritdoc/>
		public IMind Mind { get; private set; }

		/// <inheritdoc/>
		public IRelations Relations { get; private set; }

		/// <inheritdoc/>
		public IAgentBody Body { get; private set; }

		/// <inheritdoc/>
		public ITargetable Targetable { get; private set; }

		/// <inheritdoc/>
		public ITargeter Targeter { get; private set; }

		#endregion Properties

		protected override string GameObjectNamePrefix => "[Agent]";

		[SerializeField, ConstDropdown(typeof(IStateIdentifierConstants))] private string state;
		[SerializeField] private List<StateMachineGraph> brainGraphs;

		private CallbackService callbackService;
		private AEMOISettings aemoiSettings;
		private InputToActMap inputToActMap;
		private IPerformer[] performers;
		private IRelationData[] relationData;

		public void InjectDependencies(
			IAgentBody body, ITargetable targetableComponent, ITargeter targeterComponent,
			CallbackService callbackService, AEMOISettings aemoiSettings, InputToActMap inputToActMap,
			IPerformer[] performers, IRelationData[] relationData)
		{
			Body = body;
			Targetable = targetableComponent;
			Targeter = targeterComponent;
			this.callbackService = callbackService;
			this.aemoiSettings = aemoiSettings;
			this.inputToActMap = inputToActMap;
			this.performers = performers;
			this.relationData = relationData;
		}

		protected override void Awake()
		{
			base.Awake();

#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				return;
			}
#endif

			// Initialize all Agent components.
			Actor = new Actor($"ACTOR_{Identification.ID}", callbackService, inputToActMap, performers);
			Mind = new AEMOI(DependencyManager, aemoiSettings, new StatOcton(this, aemoiSettings.Personality, Vector8.Half));
			Brain = new Brain(DependencyManager, callbackService, state, null, brainGraphs);
			LoadRelations();

			// Start the Brain to become alive.
			Brain.EnteredStateEvent += OnEnteredStateEvent;
			Brain.Start();
		}

		protected void OnDestroy()
		{
			((Actor)Actor)?.Dispose();
			Brain?.Dispose();
		}

		/// <inheritdoc/>
		public void Die(ITransition transition = null)
		{
			if (Alive && Brain.TryTransition(StateIdentifiers.DEAD, transition))
			{
				Alive = false;
				Actor.TryCancel(true);
				Actor.Blocked = true;
				DiedEvent?.Invoke(this);
			}
		}

		/// <inheritdoc/>
		public void Revive(ITransition transition = null)
		{
			if (!Alive && Brain.TryTransition(StateIdentifiers.ACTIVE, transition))
			{
				Alive = true;
				Actor.Blocked = false;
				RevivedEvent?.Invoke(this);
			}
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

		private void LoadRelations()
		{
			// First load or create data collection.
			if (RuntimeData.ContainsEntry(AgentDataIdentifiers.RELATIONS))
			{
				Relations = new AgentRelations(RuntimeData.GetEntry<RuntimeDataCollection>(AgentDataIdentifiers.RELATIONS));
			}
			else
			{
				RuntimeDataCollection relationData = new RuntimeDataCollection(AgentDataIdentifiers.RELATIONS);
				RuntimeData.TryAdd(relationData, true);
				Relations = new AgentRelations(relationData);
			}

			// Populate relations with injected data.
			if (relationData != null)
			{
				foreach (IRelationData data in relationData)
				{
					var relations = data.GetRelations();
					foreach (KeyValuePair<string, float> relation in relations)
					{
						if (!Relations.Relations.ContainsKey(relation.Key))
						{
							Relations.Set(relation.Key, relation.Value);
						}
					}
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
