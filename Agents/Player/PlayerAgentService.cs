using SpaxUtils.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SpaxUtils
{
	/// <summary>
	/// Service that keeps track of player entities and the data belonging to them.
	/// </summary>
	public class PlayerAgentService : IService
	{
		private const string ID_PLAYER_COLLECTION = "PLAYER_ENTITIES";

		public event Action<IAgent> PlayerRegisteredEvent;
		public event Action<IAgent> PlayerDeregisteredEvent;

		/// <summary>
		/// All currently marked player agents.
		/// </summary>
		public IReadOnlyList<IAgent> Agents => agents;
		private List<IAgent> agents = new List<IAgent>();

		/// <summary>
		/// The <see cref="IAgent"/> of player one.
		/// </summary>
		public IAgent PlayerAgent => agents.Count > 0 ? agents[0] : null;

		private RuntimeDataService runtimeDataService;
		private CycleService cycleService;
		private CameraManager cameraManager;
		private PlayerInputService playerInputService;

		// Reverse lookup so we can unmark by IEntity/IAgent in lifecycle callbacks without scanning the list.
		// Keyed by entity ID so we don't rely on interface refs behaving nicely with Unity's fake-null.
		private Dictionary<string, int> agentIdToIndex = new Dictionary<string, int>();

		public PlayerAgentService(RuntimeDataService runtimeDataService, CycleService cycleService, CameraManager cameraManager, PlayerInputService playerInputService)
		{
			this.runtimeDataService = runtimeDataService;
			this.cycleService = cycleService;
			this.cameraManager = cameraManager;
			this.playerInputService = playerInputService;
		}

		/// <summary>
		/// Will attempt to load player entity data from the <paramref name="playerIndex"/>.
		/// </summary>
		/// <param name="playerIndex">The index of player last in control on the entity.</param>
		/// <param name="data">The resulting loaded data, if any.</param>
		/// <returns>Whether retrieving the player entity data was a success.</returns>
		public bool TryRetrievePlayerEntityData(int playerIndex, out RuntimeDataCollection data)
		{
			if (runtimeDataService.CurrentProfile != null && runtimeDataService.CurrentProfile.ContainsEntry(ID_PLAYER_COLLECTION))
			{
				List<string> playerCollection = runtimeDataService.CurrentProfile.GetValue<List<string>>(ID_PLAYER_COLLECTION);
				if (playerIndex < playerCollection.Count)
				{
					string id = playerCollection[playerIndex];
					if (runtimeDataService.CurrentProfile.ContainsEntry(id))
					{
						data = runtimeDataService.CurrentProfile.GetEntry<RuntimeDataCollection>(id);
						return true;
					}
				}
			}

			data = null;
			return false;
		}

		/// <summary>
		/// Mark an agent as being controlled by a player.
		/// </summary>
		/// <param name="agent">The agent currently under control by player <paramref name="playerIndex"/>.</param>
		/// <param name="playerIndex">The index of the player controlling the agent.</param>
		public void MarkPlayerAgent(IAgent agent, int playerIndex = -1)
		{
			if (agent == null)
			{
				SpaxDebug.Error("Can't mark player agent", "Agent is null.");
				return;
			}

			if (playerIndex < 0)
			{
				playerIndex = 0;
			}

			// Ensure slot list is large enough.
			while (agents.Count <= playerIndex)
			{
				agents.Add(null);
			}

			// If this agent is already marked under a different index, clear its old slot.
			string newId = agent.Identification != null ? agent.Identification.ID : null;
			if (!string.IsNullOrEmpty(newId) && agentIdToIndex.TryGetValue(newId, out int oldIndex) && oldIndex != playerIndex)
			{
				DismissPlayerAgent(oldIndex);
			}

			// If this slot already has a different agent, unhook it first.
			IAgent previous = agents[playerIndex];
			if (previous != null && previous != agent)
			{
				UnhookAgent(previous);
				agents[playerIndex] = null;
				PlayerDeregisteredEvent?.Invoke(previous);
			}

			agents[playerIndex] = agent;
			HookAgent(agent, playerIndex);

			// Load player collection.
			List<string> playerCollection = new List<string>();
			if (runtimeDataService.CurrentProfile.ContainsEntry(ID_PLAYER_COLLECTION))
			{
				playerCollection = runtimeDataService.CurrentProfile.GetValue<List<string>>(ID_PLAYER_COLLECTION);
			}

			while (playerCollection.Count <= playerIndex)
			{
				playerCollection.Add(string.Empty);
			}

			// Overwrite entity at player index.
			playerCollection[playerIndex] = agent.Identification.ID;
			runtimeDataService.CurrentProfile.SetValue(ID_PLAYER_COLLECTION, playerCollection);

			PlayerRegisteredEvent?.Invoke(agent);
		}

		/// <summary>
		/// Remove an agent from the tracked player agents list.
		/// </summary>
		public void DismissPlayerAgent(int index)
		{
			if (index < 0 || index >= agents.Count)
			{
				return;
			}

			IAgent agent = agents[index];
			if (agent == null)
			{
				return;
			}

			agents[index] = null;
			UnhookAgent(agent);

			PlayerDeregisteredEvent?.Invoke(agent);

			TrimTrailingNulls();
		}

		/// <summary>
		/// Remove an agent from the tracked player agents list.
		/// </summary>
		public void DismissPlayerAgent(IAgent agent)
		{
			if (agent == null)
			{
				return;
			}

			string id = agent.Identification != null ? agent.Identification.ID : null;
			if (!string.IsNullOrEmpty(id) && agentIdToIndex.TryGetValue(id, out int index))
			{
				DismissPlayerAgent(index);
				return;
			}

			// Fallback (should be rare): scan the slots.
			for (int i = 0; i < agents.Count; i++)
			{
				if (agents[i] == agent)
				{
					DismissPlayerAgent(i);
					return;
				}
			}
		}

		/// <summary>
		/// Spawns a new player agent.
		/// </summary>
		public IAgent SpawnPlayer(
			IDependencyManager dependencyManager,
			PlayerConfig config,
			AgentSpawnData spawnData,
			Transform spawnpoint,
			out List<GameObject> instances,
			Camera inputCamOverride = null,
			bool activate = true)
		{
			instances = new List<GameObject>();

			// Ensure all the required assets are present.
			if (config.AgentSetup == null ||
				config.InputActionAsset == null)
			{
				SpaxDebug.Error("One or more required player assets are missing.");
				return null;
			}

			// Create dependency managers for entities.
			DependencyManager playerDependencies = new DependencyManager(dependencyManager, "Player");
			DependencyManager cameraDependencies = null;

			// Create deactivated instances.
			GameObject camRigInstance = null;
			Camera cameraComponent = null;
			if (config.CameraPrefab != null)
			{
				camRigInstance = DependencyUtils.InstantiateDeactivated(config.CameraPrefab, spawnpoint.position, spawnpoint.rotation);
				instances.Add(camRigInstance);

				//cameraComponent = camRigInstance.GetComponentInChildren<Camera>();
				cameraComponent = cameraManager.MainCamera; // SINGLE PLAYER ONLY.
				cameraDependencies = new DependencyManager(playerDependencies, "PlayerCamera");
				playerDependencies.Bind(EntityLabels.CAMERA, cameraComponent);
				playerDependencies.Bind(cameraComponent);
				if (camRigInstance.TryGetComponentInChildren(out CineCameraWrapper cameraHandler))
				{
					playerDependencies.Bind(cameraHandler);
				}
			}
			UIRoot hudInstance = null;
			if (config.UIPrefab != null)
			{
				hudInstance = DependencyUtils.InstantiateDeactivated(config.UIPrefab.gameObject).GetComponent<UIRoot>();
				instances.Add(hudInstance.gameObject);
			}

			// Get persistent player input (NOT included in instances list).
			Camera inputCam = inputCamOverride != null ? inputCamOverride : cameraComponent != null ? cameraComponent : hudInstance != null ? hudInstance.Camera : null;

			int playerIndex = 0; // SINGLE PLAYER ONLY. When split screen exists, revisit.
			PlayerInputWrapper playerInputWrapper = playerInputService.GetOrCreate(playerIndex, config.InputActionAsset, inputCam);
			playerDependencies.Bind(playerInputWrapper);

			// Some consumers may depend on the underlying PlayerInput as well.
			if (playerInputWrapper.PlayerInput != null)
			{
				playerDependencies.Bind(playerInputWrapper.PlayerInput);
			}

			//SpaxDebug.Log($"Spawn Player [{playerInputWrapper.PlayerIndex}]");

			// Bind necessary data to dependency managers.
			RuntimeDataCollection entityData;
			if (TryRetrievePlayerEntityData(playerInputWrapper.PlayerIndex, out entityData))
			{
				playerDependencies.Bind(entityData);
			}
			else if (playerInputWrapper.PlayerIndex == 0)
			{
				// The main player character is being spawned for the first time, give it the correct data to initialize.
				entityData = new RuntimeDataCollection(config.AgentSetup.Identification.ID.IsNullOrEmpty() ? Guid.NewGuid().ToString() : config.AgentSetup.Identification.ID,
					new List<RuntimeDataEntry>()
					{
						new RuntimeDataEntry(EntityDataIdentifiers.NAME, runtimeDataService.CurrentProfile.ID)
					});
				playerDependencies.Bind(entityData);

				// Initiate the first cycle.
				cycleService.NewCycle();
			}

			// Create player setup with loaded ID, if any.
			IIdentification identification =
				entityData == null ?
					config.AgentSetup.Identification :
					new Identification(entityData.ID, config.AgentSetup.Identification.Name, config.AgentSetup.Identification.Labels, null);
			AgentSetup setup = new AgentSetup(config.AgentSetup, identification, data: entityData);

			// Create player agent.
			Agent playerAgent = spawnData.Spawn(setup, playerDependencies, spawnpoint.position, spawnpoint.rotation, activate);
			instances.Add(playerAgent.gameObject);

			// Set up player camera.
			if (camRigInstance != null)
			{
				string camName = $"PLAYER_CAMERA_{playerInputWrapper.PlayerIndex}";
				var cameraIdentification = new Identification(camName, camName, new List<string>() { EntityLabels.CAMERA }, camRigInstance.GetComponent<IEntity>());
				cameraDependencies.Bind(cameraIdentification);
				DependencyUtils.BindMonoBehaviours(camRigInstance, cameraDependencies, includeChildren: true);
				DependencyUtils.Inject(camRigInstance, cameraDependencies, includeChildren: true, bindComponents: false);
				camRigInstance.SetActive(true);
			}

			// Set up UI.
			if (hudInstance != null)
			{
				DependencyManager hudDependencies = new DependencyManager(playerDependencies, "PlayerHUD");
				DependencyUtils.BindMonoBehaviours(hudInstance.gameObject, hudDependencies, includeChildren: true);
				DependencyUtils.Inject(hudInstance.gameObject, hudDependencies, includeChildren: true, bindComponents: false);
				hudInstance.gameObject.SetActive(true);

				Camera uiCamera = hudInstance.GetComponentInChildren<Camera>();
				if (uiCamera != null)
				{
					// Add UI camera to main camera stack.
					var cameraData = cameraComponent.GetUniversalAdditionalCameraData();
					cameraData.cameraStack.Add(uiCamera);
				}
			}

			MarkPlayerAgent(playerAgent, playerInputWrapper.PlayerIndex);
			return playerAgent;
		}

		/// <summary>
		/// Returns whether the player with index <paramref name="playerIndex"/> is currently alive, whether actively or in its runtime data.
		/// </summary>
		public bool IsAlive(int playerIndex = 0)
		{
			if (agents.Count > playerIndex)
			{
				IAgent a = agents[playerIndex];

				if (a != null)
				{
					// If IAgent is backed by a UnityEngine.Object, handle destroyed refs too.
					if (!(a is UnityEngine.Object uo) || uo)
					{
						return a.Alive;
					}
				}
			}

			if (TryRetrievePlayerEntityData(playerIndex, out RuntimeDataCollection playerData))
			{
				return playerData.GetValue(EntityDataIdentifiers.ALIVE, false);
			}

			return false;
		}

		/// <summary>
		/// Returns the squared distance to the closest player agent.
		/// </summary>
		public float GetSqrDistanceToClosestPlayer(Vector3 point, out IAgent closest)
		{
			closest = null;
			float closestDistance = float.MaxValue;
			foreach (IAgent player in agents)
			{
				if (player == null)
				{
					continue;
				}

				float distance = (player.Transform.position - point).sqrMagnitude;
				if (distance < closestDistance)
				{
					closest = player;
					closestDistance = distance;
				}
			}

			return closestDistance;
		}

		/// <summary>
		/// Returns the distance to the closest player agent.
		/// </summary>
		public float GetDistanceToClosestPlayer(Vector3 point, out IAgent closest)
		{
			return GetSqrDistanceToClosestPlayer(point, out closest).Sqrt();
		}

		private void HookAgent(IAgent agent, int index)
		{
			string id = agent.Identification != null ? agent.Identification.ID : null;
			if (!string.IsNullOrEmpty(id))
			{
				agentIdToIndex[id] = index;
			}

			if (agent is IEntity entity)
			{
				entity.DeactivatedEvent += OnAgentDeactivatedEvent;
			}
		}

		private void UnhookAgent(IAgent agent)
		{
			string id = agent.Identification != null ? agent.Identification.ID : null;
			if (!string.IsNullOrEmpty(id))
			{
				agentIdToIndex.Remove(id);
			}

			if (agent is IEntity entity)
			{
				entity.DeactivatedEvent -= OnAgentDeactivatedEvent;
			}
		}

		private void OnAgentDeactivatedEvent(IEntity entity)
		{
			IAgent agent = entity as IAgent;
			if (agent == null)
			{
				return;
			}

			DismissPlayerAgent(agent);
		}

		private void TrimTrailingNulls()
		{
			for (int i = agents.Count - 1; i >= 0; i--)
			{
				if (agents[i] != null)
				{
					return;
				}

				agents.RemoveAt(i);
			}
		}
	}
}
