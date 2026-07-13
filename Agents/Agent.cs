using SpaxUtils.StateMachines;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Base <see cref="IAgent"/> implementation.
	/// </summary>
	public class Agent : Entity, IAgent
	{
		public event Action<DeathContext> DiedEvent;
		public event Action ReviveEvent;
		public event Action RecoverEvent;

		#region Properties

		/// <inheritdoc/>
		public bool Alive { get; protected set; }

		/// <inheritdoc/>
		public float Age => (float)_age;
		private double _age;

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

		/// <inheritdoc/>
		public ICommunicationChannel Comms { get; private set; }

		#endregion Properties

		protected override string GameObjectNamePrefix => "[Agent]";

		[Header("Agent")]
		[SerializeField, ConstDropdown(typeof(IStateIdentifiers))] private string state;
		[SerializeField] private List<BrainGraph> brainGraphs;

		private IRelationData[] relationData;
		private WorldRegion region;

		public void InjectDependencies(
			IAgentBody body, ITargetable targetableComponent, ITargeter targeterComponent, ICommunicationChannel comms,
			CallbackService callbackService, InputToActMap inputToActMap,
			IPerformer[] performers, IRelationData[] relationData, BrainGraph[] brainGraphs,
			[Optional] IMind mind, [Optional] ISpawnpoint spawnpoint)
		{
			Body = body;
			Targetable = targetableComponent;
			Targeter = targeterComponent;
			Comms = comms;
			Mind = mind;
			region = spawnpoint?.Region;

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

			Actor = new Actor($"ACTOR_{Identification.ID}", callbackService, inputToActMap, performers,
				(state) => Brain != null && Brain.IsStateActive(state));
			Brain = new Brain(DependencyManager, callbackService, state, null, brainGraphs);
			LoadRelations();

			// Bind Agent components so that later injections can retrieve them easily (this is meant for Nodes, not EntityComponents as they may already be injected before this).
			DependencyManager.Bind(Actor);
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

			// Mirror the activity of the region this agent was spawned in, if any.
			if (region != null)
			{
				region.ActivityChangedEvent += OnRegionActivityChanged;
			}
		}

		protected override void Update()
		{
			base.Update();

			if (Alive)
			{
				_age += Time.deltaTime;
			}
		}

		protected override void OnDestroy()
		{
			if (region != null)
			{
				region.ActivityChangedEvent -= OnRegionActivityChanged;
			}
			((Actor)Actor)?.Dispose();
			Brain?.Dispose();
			Mind?.Dispose();
			base.OnDestroy();
		}

		protected override void ApplyData()
		{
			base.ApplyData();

			// Retrieve whether this entity was last alive when its data was saved.
			Alive = RuntimeData.GetValue(EntityDataIdentifiers.ALIVE, false);
			_age = RuntimeData.GetValue(EntityDataIdentifiers.AGE, 0d);
			if (Alive && RuntimeData.GetValue<string>(EntityDataIdentifiers.SCENE) == sceneService.CurrentScene)
			{
				Transform.position = RuntimeData.GetValue(EntityDataIdentifiers.POSITION, transform.position);
				Transform.eulerAngles = RuntimeData.GetValue(EntityDataIdentifiers.ROTATION, transform.eulerAngles);
			}
			Alive = true; // Entity is being initialized so it is now definitely alive.
		}

		protected override void OnSavingData()
		{
			base.OnSavingData();
			RuntimeData.SetValue(EntityDataIdentifiers.ALIVE, Alive);
			RuntimeData.SetValue(EntityDataIdentifiers.AGE, _age);
			RuntimeData.SetValue(EntityDataIdentifiers.SCENE, sceneService.CurrentScene);
			RuntimeData.SetValue(EntityDataIdentifiers.POSITION, Transform.position);
			RuntimeData.SetValue(EntityDataIdentifiers.ROTATION, Transform.eulerAngles);
		}

		/// <inheritdoc/>
		public void Die(DeathContext context)
		{
			if (!Alive)
			{
				return;
			}

			Alive = false;
			Actor.TryCancel(true);
			Actor.AddBlocker(this);
			Brain.TryTransition(AgentStateIdentifiers.DEAD);
			DiedEvent?.Invoke(context);
		}

		/// <inheritdoc/>
		public void Revive()
		{
			if (Alive)
			{
				// Already alive; ReviveEvent fires unconditionally below, so without this a second Revive would run
				// the death handler's resolve again (double-disposing its modifiers).
				return;
			}

			// Restore from the death state (clearing the death timescale/control/fade modifiers) BEFORE transitioning,
			// so state-entry behaviours (e.g. arm sheathing) run at normal speed instead of the lingering death slow-mo.
			Recover();
			ReviveEvent?.Invoke();

			// Target Control (Active's autonomous default child), not Active: Dead is a child of Active, so
			// IsStateActive(Active) is true while dead and a transition to Active would no-op, stranding us in Dead.
			if (!Alive && (Brain.IsStateActive(AgentStateIdentifiers.CONTROL) || Brain.TryTransition(AgentStateIdentifiers.CONTROL)))
			{
				Alive = true;
				Actor.RemoveBlocker(this);
			}
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

		private void OnRegionActivityChanged(WorldRegion.RegionActivity activity)
		{
			// Only mirror region activity while alive; dead agents stay dead until respawned.
			if (!Alive)
			{
				return;
			}
			string target = RegionActivityToBrainState(activity);
			// Guard so we don't re-enter (and tear down) a state the agent is already in.
			if (!Brain.IsStateActive(target))
			{
				Brain.TryTransition(target);
			}
		}

		/// <summary>Maps a <see cref="WorldRegion.RegionActivity"/> to the brain state that mirrors it.</summary>
		public static string RegionActivityToBrainState(WorldRegion.RegionActivity activity)
		{
			switch (activity)
			{
				case WorldRegion.RegionActivity.Sleep: return AgentStateIdentifiers.SLEEP;
				case WorldRegion.RegionActivity.Inactive: return AgentStateIdentifiers.INACTIVE;
				// Active maps to Control (Active's default child) rather than Active itself: since Sleep is
				// also a child of Active, targeting Active would read as "already active" and skip the wake.
				default: return AgentStateIdentifiers.CONTROL;
			}
		}
	}
}
