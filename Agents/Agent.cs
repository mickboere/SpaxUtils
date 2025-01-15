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
		public event Action ReviveEvent;
		public event Action RecoverEvent;

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

		[Header("Agent")]
		[SerializeField, ConstDropdown(typeof(IStateIdentifiers))] private string state;
		[SerializeField] private List<BrainGraph> brainGraphs;

		private IRelationData[] relationData;

		public void InjectDependencies(
			IAgentBody body, ITargetable targetableComponent, ITargeter targeterComponent,
			CallbackService callbackService, AEMOISettings aemoiSettings, InputToActMap inputToActMap,
			IPerformer[] performers, IRelationData[] relationData, BrainGraph[] brainGraphs,
			[Optional, BindingIdentifier(AgentDataIdentifiers.PERSONALITY)] Vector8 personality)
		{
			Body = body;
			Targetable = targetableComponent;
			Targeter = targeterComponent;

			this.relationData = relationData;

			foreach (BrainGraph brainGraph in brainGraphs)
			{
				if (!this.brainGraphs.Contains(brainGraph))
				{
					this.brainGraphs.Add(brainGraph);
				}
			}

			if (Actor != null)
			{
				SpaxDebug.Error($"[{Identification.ID}] Double injection on Agent!");
				return;
			}

			// Initialize all Agent components.
			Actor = new Actor($"ACTOR_{Identification.ID}", callbackService, inputToActMap, performers);
			Brain = new Brain(DependencyManager, callbackService, state, null, brainGraphs);
			Mind = new AEMOI(DependencyManager, aemoiSettings, new StatOctad(this, aemoiSettings.Personality, personality == Vector8.Zero ? Vector8.Half : personality));
			LoadRelations();

			// Bind Agent components so that later injections can retrieve them easily (this is meant for Nodes, not EntityComponents as they may already be injected before this).
			DependencyManager.Bind(Actor);
			DependencyManager.Bind(Mind);
			DependencyManager.Bind(Brain);
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

			// Start the Brain to come to life.
			Brain.EnteredStateEvent += OnEnteredStateEvent;
			Brain.Start();
		}

		protected override void OnDestroy()
		{
			((Actor)Actor)?.Dispose();
			Brain?.Dispose();
			Mind?.Dispose();
			base.OnDestroy();
		}

		/// <inheritdoc/>
		public void Die(ITransition transition = null)
		{
			if (Alive && Brain.TryTransition(AgentStateIdentifiers.DEAD, transition))
			{
				Alive = false;
				Actor.TryCancel(true);
				Actor.AddBlocker(this);
				DiedEvent?.Invoke(this);
			}
		}

		/// <inheritdoc/>
		public void Revive(ITransition transition = null)
		{
			if (!Alive && (Brain.IsStateActive(AgentStateIdentifiers.ACTIVE) || Brain.TryTransition(AgentStateIdentifiers.ACTIVE, transition)))
			{
				Alive = true;
				Actor.RemoveBlocker(this);
			}

			Recover();

			ReviveEvent?.Invoke();
		}

		/// <inheritdoc/>
		public void Recover()
		{
			RecoverEvent?.Invoke();
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
			//SpaxDebug.Notify($"[{Identification.Name}]", $"OnEnteredStateEvent({string.Join(", ", Brain.StateHierarchy.Select(s => s.ID))})");
		}
	}
}
