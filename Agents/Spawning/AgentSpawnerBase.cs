using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[RequireComponent(typeof(Entity))]
	public abstract class AgentSpawnerBase : EntityComponentMono
	{
		[Header("Spawner")]
		[SerializeField] protected AgentSetupAsset agentSetup;
		[SerializeField] protected AgentSpawnData spawnData;

		[Header("Cycle Policy")]
		[SerializeField] private bool dataPersists;

		private IDependencyManager dependencyManager;
		private RuntimeDataService runtimeDataService;
		private WorldService worldService;

		// Owned instances by slot id.
		protected Dictionary<string, Agent> spawned = new Dictionary<string, Agent>();

		public void InjectDependencies(
			IDependencyManager dependencyManager,
			RuntimeDataService runtimeDataService,
			WorldService worldService)
		{
			this.dependencyManager = dependencyManager;
			this.runtimeDataService = runtimeDataService;
			this.worldService = worldService;
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

		protected string GetSlotId(int slotIndex)
		{
			return Entity.ID + "_" + slotIndex;
		}

		protected abstract int GetSlotCount();
		protected abstract bool TryGetSpawnpoint(int slotIndex, out ISpawnpoint spawnpoint);
	}
}
