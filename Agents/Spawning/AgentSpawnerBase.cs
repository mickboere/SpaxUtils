using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[RequireComponent(typeof(Entity))]
	public abstract class AgentSpawnerBase : EntityComponentMono
	{
		private const string REQ_MET_KEY = "ReqMet";

		[Header("Flag Requirements")]
		[SerializeField, Tooltip("All requirements must be met before agents are spawned. Evaluated on first session and on each new cycle.")]
		private FlagRequirement[] spawnRequirements;

		[Header("Flag Setters")]
		[SerializeField, Tooltip("Applied once all agents belonging to this spawner are dead.")]
		private FlagSetting[] deathFlags;

		[Header("Spawner")]
		[SerializeField] protected AgentSetupAsset agentSetup;
		[SerializeField] protected AgentSpawnData spawnData;

		[Header("Cycle Policy")]
		[SerializeField] private bool dataPersists;

		private IDependencyManager dependencyManager;
		private RuntimeDataService runtimeDataService;
		private WorldService worldService;
		private FlagService flagService;

		private bool requirementsMet;
		private bool deathFlagsApplied;

		// Owned instances by slot id.
		protected Dictionary<string, Agent> spawned = new Dictionary<string, Agent>();

		public void InjectDependencies(
			IDependencyManager dependencyManager,
			RuntimeDataService runtimeDataService,
			WorldService worldService,
			FlagService flagService)
		{
			this.dependencyManager = dependencyManager;
			this.runtimeDataService = runtimeDataService;
			this.worldService = worldService;
			this.flagService = flagService;
		}

		protected virtual void Start()
		{
			if (agentSetup == null || spawnData == null)
			{
				SpaxDebug.Error("Spawner missing setup/spawnData.", $"Spawner='{Entity.ID}'", gameObject);
				return;
			}

			runtimeDataService.SavingToDiskEvent += OnSavingToDiskEvent;

			worldService.WorldActiveChangedEvent += OnWorldActiveChangedEvent;
			worldService.NewCycleEvent += OnNewCycleEvent;

			// Determine requirements-met state.
			// On first session (no stored value): evaluate and store.
			// On reload (stored value exists): use stored value.
			if (TryGetStoredRequirementsMet(out bool storedMet))
			{
				requirementsMet = storedMet;
			}
			else
			{
				requirementsMet = FlagRequirement.EvaluateAll(spawnRequirements, flagService);
				StoreRequirementsMet(requirementsMet);
			}

			deathFlagsApplied = false;

			// Initial state.
			if (worldService.WorldActive)
			{
				EnsureSpawnedOrActivated();
			}
			else
			{
				DeactivateAll();
			}
		}

		protected virtual void OnDestroy()
		{
			if (runtimeDataService != null)
			{
				runtimeDataService.SavingToDiskEvent -= OnSavingToDiskEvent;
			}

			if (worldService != null)
			{
				worldService.WorldActiveChangedEvent -= OnWorldActiveChangedEvent;
				worldService.NewCycleEvent -= OnNewCycleEvent;
			}

			UnsubscribeAllDeathEvents();
		}

		private void OnWorldActiveChangedEvent(bool active)
		{
			if (active)
			{
				EnsureSpawnedOrActivated();
			}
			else
			{
				DeactivateAll();
			}
		}

		private void OnNewCycleEvent(int cycle)
		{
			// Destroy all owned agent instances (regardless of alive/dead) behind loading screen.
			DestroyAllOwnedAgents();

			// Optionally wipe their saved runtime data so they reroll on next spawn.
			if (!dataPersists)
			{
				ClearSlotRuntimeDataFromProfile();
			}

			// Re-evaluate requirements on each new cycle.
			requirementsMet = FlagRequirement.EvaluateAll(spawnRequirements, flagService);
			StoreRequirementsMet(requirementsMet);
			deathFlagsApplied = false;

			// If the world is active right now, respawn immediately.
			if (worldService.WorldActive)
			{
				EnsureSpawnedOrActivated();
			}
		}

		private void OnSavingToDiskEvent(RuntimeDataCollection profile)
		{
			// Spawner decides when agent data is saved: on disk-save, save all owned agents (even inactive).
			foreach (KeyValuePair<string, Agent> kvp in spawned)
			{
				Agent a = kvp.Value;
				if (a != null)
				{
					a.SaveData();
				}
			}
		}

		private void EnsureSpawnedOrActivated()
		{
			if (!requirementsMet)
			{
				return;
			}

			int slots = GetSlotCount();
			for (int i = 0; i < slots; i++)
			{
				string slotId = GetSlotId(i);

				// If we already have an instance, only reactivate if it's alive.
				if (spawned.TryGetValue(slotId, out Agent existing) && existing != null)
				{
					bool isDead = !existing.Alive;
					if (!isDead && !existing.gameObject.activeSelf)
					{
						existing.gameObject.SetActive(true);
					}
					continue;
				}

				// If saved data says "dead", do not spawn.
				if (IsSlotDeadInProfile(slotId))
				{
					continue;
				}

				// Spawn new instance for this slot.
				if (TryGetSpawnpoint(i, out ISpawnpoint spawnpoint))
				{
					Agent agent = SpawnSlot(slotId, spawnpoint);
					if (agent != null)
					{
						spawned[slotId] = agent;
						agent.DiedEvent += OnAgentDied;
					}
				}
				else
				{
					SpaxDebug.Warning("Spawner missing spawnpoint.", $"Slot={i}, Spawner='{Entity.ID}'", gameObject);
				}
			}
		}

		private void DeactivateAll()
		{
			foreach (KeyValuePair<string, Agent> kvp in spawned)
			{
				Agent a = kvp.Value;
				if (a != null && a.gameObject.activeSelf)
				{
					a.gameObject.SetActive(false);
				}
			}
		}

		private void DestroyAllOwnedAgents()
		{
			// Copy keys first because destroying may mutate.
			List<string> keys = new List<string>(spawned.Keys);
			foreach (string slotId in keys)
			{
				Agent a = spawned[slotId];
				if (a != null)
				{
					a.DiedEvent -= OnAgentDied;
					UnityEngine.Object.Destroy(a.gameObject);
				}
				spawned[slotId] = null;
			}
		}

		private void ClearSlotRuntimeDataFromProfile()
		{
			if (runtimeDataService == null || runtimeDataService.CurrentProfile == null)
			{
				return;
			}

			int slots = GetSlotCount();
			for (int i = 0; i < slots; i++)
			{
				string slotId = GetSlotId(i);
				runtimeDataService.CurrentProfile.TryRemove(slotId, dispose: true);
			}
		}

		private bool IsSlotDeadInProfile(string slotId)
		{
			if (runtimeDataService == null || runtimeDataService.CurrentProfile == null)
			{
				return false;
			}

			if (runtimeDataService.CurrentProfile.TryGetEntry(slotId, out RuntimeDataCollection data))
			{
				// Dead is derived from Alive.
				return data.GetValue(EntityDataIdentifiers.ALIVE, false) == false && data.ContainsEntry(EntityDataIdentifiers.ALIVE);
			}

			return false;
		}

		private Agent SpawnSlot(string slotId, ISpawnpoint spawnpoint)
		{
			DependencyManager agentDependencyManager = new DependencyManager(dependencyManager, slotId);
			agentDependencyManager.Bind(spawnpoint);

			// Override identification to use deterministic slot id.
			IIdentification id = new Identification(
				slotId,
				agentSetup.Identification.Name,
				agentSetup.Identification.Labels,
				null);

			IAgentSetup setup = new AgentSetup(agentSetup, id, data: null);

			Agent agent = spawnData.Spawn(setup, agentDependencyManager,
				spawnpoint.Position, spawnpoint.Rotation, worldService.WorldActive);
			agent.Brain.TryTransition(AgentStateIdentifiers.ACTIVE);
			return agent;
		}

		#region Flag Requirements

		private bool TryGetStoredRequirementsMet(out bool value)
		{
			value = false;

			if (runtimeDataService == null || runtimeDataService.CurrentProfile == null)
			{
				return false;
			}

			if (!runtimeDataService.CurrentProfile.TryGetEntry(Entity.ID, out RuntimeDataCollection data))
			{
				return false;
			}

			if (!data.ContainsEntry(REQ_MET_KEY))
			{
				return false;
			}

			value = data.GetValue<bool>(REQ_MET_KEY, false);
			return true;
		}

		private void StoreRequirementsMet(bool value)
		{
			if (runtimeDataService == null || runtimeDataService.CurrentProfile == null)
			{
				return;
			}

			if (!runtimeDataService.CurrentProfile.TryGetEntry(Entity.ID, out RuntimeDataCollection data))
			{
				data = new RuntimeDataCollection(Entity.ID);
				runtimeDataService.CurrentProfile.TryAdd(data);
			}

			data.SetValue(REQ_MET_KEY, value);
		}

		#endregion Flag Requirements

		#region Death Flags

		private void OnAgentDied(DeathContext context)
		{
			if (deathFlagsApplied)
			{
				return;
			}

			if (deathFlags == null || deathFlags.Length == 0)
			{
				return;
			}

			if (AreAllSlotsDead())
			{
				deathFlagsApplied = true;
				FlagSetting.ApplyAll(deathFlags, flagService);
			}
		}

		private bool AreAllSlotsDead()
		{
			int slots = GetSlotCount();
			if (slots == 0)
			{
				return false;
			}

			for (int i = 0; i < slots; i++)
			{
				string slotId = GetSlotId(i);

				// Check live instance first.
				if (spawned.TryGetValue(slotId, out Agent agent) && agent != null)
				{
					if (agent.Alive)
					{
						return false;
					}
					continue; // Dead in play.
				}

				// No live instance - check profile.
				if (!IsSlotDeadInProfile(slotId))
				{
					return false; // Not confirmed dead anywhere.
				}
			}

			return true;
		}

		private void UnsubscribeAllDeathEvents()
		{
			foreach (KeyValuePair<string, Agent> kvp in spawned)
			{
				Agent a = kvp.Value;
				if (a != null)
				{
					a.DiedEvent -= OnAgentDied;
				}
			}
		}

		#endregion Death Flags

		protected string GetSlotId(int slotIndex)
		{
			return Entity.ID + "_" + slotIndex;
		}

		protected abstract int GetSlotCount();
		protected abstract bool TryGetSpawnpoint(int slotIndex, out ISpawnpoint spawnpoint);
	}
}
