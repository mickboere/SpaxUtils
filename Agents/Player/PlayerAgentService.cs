using SpaxUtils.UI;
using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;

namespace SpaxUtils
{
	/// <summary>
	/// Service that keeps track of player entities and the data belonging to them.
	/// </summary>
	public class PlayerAgentService : IService
	{
		private const string ID_PLAYER_COLLECTION = "PLAYER_ENTITIES";
		private const string PLAYER_ID = "PLAYER";

		public event Action<IAgent> PlayerRegisteredEvent;
		public event Action<IAgent> PlayerDeregisteredEvent;

		/// <summary>
		/// Invoked when a player asks to leave the game, with that player's index.
		/// </summary>
		public event Action<int> LeaveRequestedEvent;

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
		private WorldService cycleService;
		private CameraManager cameraManager;
		private PlayerInputService playerInputService;
		private SplitScreenService splitScreenService;

		// Reverse lookup so we can unmark by IEntity/IAgent in lifecycle callbacks without scanning the list.
		// Keyed by entity ID so we don't rely on interface refs behaving nicely with Unity's fake-null.
		private Dictionary<string, int> agentIdToIndex = new Dictionary<string, int>();

		private List<EventSystem> playerEventSystems = new List<EventSystem>();

		public PlayerAgentService(RuntimeDataService runtimeDataService, WorldService cycleService, CameraManager cameraManager,
			PlayerInputService playerInputService, SplitScreenService splitScreenService)
		{
			this.runtimeDataService = runtimeDataService;
			this.cycleService = cycleService;
			this.cameraManager = cameraManager;
			this.playerInputService = playerInputService;
			this.splitScreenService = splitScreenService;
		}

		public static string GetPlayerId(int playerIndex)
		{
			if (playerIndex < 0)
			{
				SpaxDebug.Error("Player index out of range.");
				return null;
			}

			if (playerIndex == 0)
			{
				return PLAYER_ID;
			}

			return PLAYER_ID + $"_{playerIndex + 1}";
		}

		/// <summary>
		/// The name a joined player goes by until renamed.
		/// </summary>
		public static string GetDefaultName(int playerIndex)
		{
			return $"Player {playerIndex + 1}";
		}

		/// <summary>
		/// Will attempt to load player entity data for <paramref name="playerIndex"/>.
		/// Uses deterministic player IDs (PLAYER_0, PLAYER_1, ...).
		/// </summary>
		public bool TryRetrievePlayerEntityData(int playerIndex, out RuntimeDataCollection data)
		{
			data = null;

			if (runtimeDataService.CurrentProfile == null)
			{
				return false;
			}

			string deterministicId = GetPlayerId(playerIndex);
			if (runtimeDataService.CurrentProfile.ContainsEntry(deterministicId))
			{
				data = runtimeDataService.CurrentProfile.GetEntry<RuntimeDataCollection>(deterministicId);
				return true;
			}

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

			// Store deterministic mapping in the profile collection.
			if (runtimeDataService.CurrentProfile != null)
			{
				List<string> playerCollection = new List<string>();
				if (runtimeDataService.CurrentProfile.ContainsEntry(ID_PLAYER_COLLECTION))
				{
					playerCollection = runtimeDataService.CurrentProfile.GetValue<List<string>>(ID_PLAYER_COLLECTION);
				}

				if (playerCollection == null)
				{
					playerCollection = new List<string>();
				}

				while (playerCollection.Count <= playerIndex)
				{
					playerCollection.Add(string.Empty);
				}

				playerCollection[playerIndex] = GetPlayerId(playerIndex);
				runtimeDataService.CurrentProfile.SetValue(ID_PLAYER_COLLECTION, playerCollection);
			}

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
		/// Asks whoever maintains the players to remove player <paramref name="playerIndex"/> from the game.
		/// </summary>
		public void RequestLeave(int playerIndex)
		{
			LeaveRequestedEvent?.Invoke(playerIndex);
		}

		/// <summary>
		/// Writes every active player's data into the current profile, optionally skipping <paramref name="except"/>.
		/// </summary>
		public void SaveAllPlayers(IAgent except = null)
		{
			foreach (IAgent agent in agents)
			{
				if (agent != except && agent.Exists())
				{
					agent.SaveData();
				}
			}
		}

		/// <summary>
		/// Spawns player <paramref name="playerIndex"/>, adding every created instance to <paramref name="instances"/>.
		/// </summary>
		public IAgent SpawnPlayer(
			IDependencyManager dependencyManager,
			PlayerConfig config,
			AgentSpawnData spawnData,
			Vector3 position,
			Quaternion rotation,
			int playerIndex,
			List<GameObject> instances,
			Camera inputCamOverride = null)
		{
			// Ensure all the required assets are present.
			if (config.AgentSetup == null ||
				config.InputActionAsset == null)
			{
				SpaxDebug.Error("One or more required player assets are missing.");
				return null;
			}

			string deterministicPlayerId = GetPlayerId(playerIndex);

			// A joining player without data of their own starts as a copy of player one. Resolved before anything is
			// instantiated: falling through to the ID fallback below would give them player one's ID.
			RuntimeDataCollection seedData = null;
			if (playerIndex > 0 && !TryRetrievePlayerEntityData(playerIndex, out _))
			{
				seedData = CreateSeedData(playerIndex);
				if (seedData == null)
				{
					SpaxDebug.Error("Can't spawn player.", $"Player {playerIndex + 1} has no data and there is no player one to copy.");
					return null;
				}
			}

			// Create dependency managers for entities.
			DependencyManager playerDependencies = new DependencyManager(dependencyManager, "Player");
			DependencyManager cameraDependencies = null;

			// Create deactivated instances.
			GameObject camRigInstance = null;
			Camera cameraComponent = null;
			MainCameraHandler cameraHandler = null;
			if (config.CameraPrefab != null)
			{
				camRigInstance = DependencyUtils.InstantiateDeactivated(config.CameraPrefab, position, rotation);
				instances.Add(camRigInstance);

				// The rig is inactive until it's injected, so every lookup must include inactive children.
				cameraHandler = camRigInstance.GetComponentInChildren<MainCameraHandler>(true);
				if (cameraHandler != null && cameraHandler.Camera != null)
				{
					cameraComponent = cameraHandler.Camera;
					foreach (CinemachineVirtualCameraBase vcam in camRigInstance.GetComponentsInChildren<CinemachineVirtualCameraBase>(true))
					{
						vcam.OutputChannel = CameraManager.GetPlayerChannel(playerIndex);
					}
				}
				else
				{
					cameraHandler = null;
					cameraComponent = cameraManager.PrimaryCamera;
					SpaxDebug.Error("Player camera rig has no camera of its own.",
						$"'{config.CameraPrefab.name}' needs a child with Camera, CinemachineBrain and MainCameraHandler. Using the main camera.");
				}

				cameraDependencies = new DependencyManager(playerDependencies, "PlayerCamera");
				playerDependencies.Bind(EntityLabels.CAMERA, cameraComponent);
				playerDependencies.Bind(cameraComponent);

				CineCameraWrapper cineCameraWrapper = camRigInstance.GetComponentInChildren<CineCameraWrapper>(true);
				if (cineCameraWrapper != null)
				{
					playerDependencies.Bind(cineCameraWrapper);
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

			PlayerInputWrapper playerInputWrapper = playerInputService.GetOrCreate(playerIndex, config.InputActionAsset, inputCam);
			playerDependencies.Bind(playerInputWrapper);

			// Some consumers may depend on the underlying PlayerInput as well.
			if (playerInputWrapper.PlayerInput != null)
			{
				playerDependencies.Bind(playerInputWrapper.PlayerInput);
			}

			// Per-player UI: own selection/navigation and own dialogue box, so simultaneous menus don't collide.
			// Player one keeps the global dialogue box, which world-side flows (NPC dialogue) also show in.
			if (hudInstance != null)
			{
				CreatePlayerEventSystem(config, hudInstance, playerInputWrapper, playerDependencies, instances);
				if (playerIndex > 0)
				{
					playerDependencies.Bind(new DialogueBoxService());
				}
			}

			// Bind necessary data to dependency managers.
			RuntimeDataCollection entityData;
			if (TryRetrievePlayerEntityData(playerIndex, out entityData))
			{
				playerDependencies.Bind(entityData);
			}
			else if (seedData != null)
			{
				entityData = seedData;
				playerDependencies.Bind(entityData);
			}
			else if (playerIndex == 0)
			{
				// The main player character is being spawned for the first time, give it deterministic ID and profile-based name.
				entityData = new RuntimeDataCollection(
					deterministicPlayerId,
					new List<RuntimeDataEntry>()
					{
						new RuntimeDataEntry(EntityDataIdentifiers.NAME, runtimeDataService.CurrentProfile != null ? runtimeDataService.GetProfileName() : string.Empty)
					});
				playerDependencies.Bind(entityData);

				// Initiate the first cycle.
				cycleService.NewCycle();
			}

			// Create player setup with deterministic ID and profile-based name.
			string desiredPlayerName = GetDisplayName(playerIndex, config);

			IIdentification identification =
				entityData == null ?
					new Identification(deterministicPlayerId, desiredPlayerName, config.AgentSetup.Identification.Labels, null) :
					new Identification(entityData.ID, desiredPlayerName, config.AgentSetup.Identification.Labels, null);

			AgentSetup setup = new AgentSetup(config.AgentSetup, identification, data: entityData);

			// Create player agent.
			Agent playerAgent = spawnData.Spawn(setup, playerDependencies, position, rotation);
			instances.Add(playerAgent.gameObject);

			// Set up player camera.
			if (camRigInstance != null)
			{
				string camName = "PLAYER_CAMERA_" + playerIndex;
				var cameraIdentification = new Identification(camName, camName, new List<string>() { EntityLabels.CAMERA }, camRigInstance.GetComponentInChildren<IEntity>(true));
				cameraDependencies.Bind(cameraIdentification);
				DependencyUtils.BindMonoBehaviours(camRigInstance, cameraDependencies, includeChildren: true);
				DependencyUtils.Inject(camRigInstance, cameraDependencies, includeChildren: true, bindComponents: false);
				camRigInstance.SetActive(true);

				if (cameraHandler != null)
				{
					cameraManager.RegisterPlayerCamera(playerIndex, cameraHandler);
				}
			}

			// Set up UI.
			if (hudInstance != null)
			{
				DependencyManager hudDependencies = new DependencyManager(playerDependencies, "PlayerHUD");
				DependencyUtils.BindMonoBehaviours(hudInstance.gameObject, hudDependencies, includeChildren: true);
				DependencyUtils.Inject(hudInstance.gameObject, hudDependencies, includeChildren: true, bindComponents: false);
				hudInstance.gameObject.SetActive(true);

				Camera uiCamera = hudInstance.GetComponentInChildren<Camera>();
				if (uiCamera != null && cameraComponent != null)
				{
					// Add UI camera to main camera stack.
					var cameraData = cameraComponent.GetUniversalAdditionalCameraData();
					cameraData.cameraStack.Add(uiCamera);
				}
			}

			MarkPlayerAgent(playerAgent, playerIndex);
			SetPlayersHostile();

			// Reapply viewports now the UI camera is stacked too.
			splitScreenService.Relayout();

			return playerAgent;
		}

		/// <summary>
		/// Returns whether the player with index <paramref name="playerIndex"/> is currently alive, either actively or in its runtime data.
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

		private string GetDisplayName(int playerIndex, PlayerConfig config)
		{
			if (playerIndex > 0)
			{
				return GetDefaultName(playerIndex);
			}

			return runtimeDataService.CurrentProfile != null ? runtimeDataService.GetProfileName() : config.AgentSetup.Identification.Name;
		}

		/// <summary>
		/// Copies player one's live data for a joining player; live because gear and stats aren't written on save.
		/// </summary>
		private RuntimeDataCollection CreateSeedData(int playerIndex)
		{
			RuntimeDataCollection source = PlayerAgent.Exists() ? PlayerAgent.RuntimeData : null;
			if (source == null && !TryRetrievePlayerEntityData(0, out source))
			{
				return null;
			}

			RuntimeDataCollection seed = source.CloneCollection(GetPlayerId(playerIndex));
			seed.SetValue(EntityDataIdentifiers.NAME, GetDefaultName(playerIndex));

			// Dead in data: skips the saved-position restore (the spawn position wins) and triggers a full recover.
			seed.SetValue(EntityDataIdentifiers.ALIVE, false);
			return seed;
		}

		private void CreatePlayerEventSystem(PlayerConfig config, UIRoot hudInstance, PlayerInputWrapper playerInputWrapper,
			DependencyManager playerDependencies, List<GameObject> instances)
		{
			if (config.PlayerEventSystemPrefab == null)
			{
				return;
			}

			GameObject instance = DependencyUtils.InstantiateDeactivated(config.PlayerEventSystemPrefab.gameObject);
			instances.Add(instance);

			MultiplayerEventSystem eventSystem = instance.GetComponent<MultiplayerEventSystem>();
			eventSystem.playerRoot = hudInstance.gameObject;
			if (instance.TryGetComponent(out InputSystemUIInputModule inputModule) && playerInputWrapper.PlayerInput != null)
			{
				// Points the module at this player's own (cloned) actions.
				playerInputWrapper.PlayerInput.uiInputModule = inputModule;
			}
			playerDependencies.Bind(typeof(EventSystem), eventSystem);

			playerEventSystems.Add(eventSystem);
			DestroyNotifier.Get(instance).DestroyedEvent += () =>
			{
				playerEventSystems.Remove(eventSystem);
				RefreshGlobalEventSystem();
			};
			RefreshGlobalEventSystem();

			instance.SetActive(true);
		}

		// A plain EventSystem has no player root and would raycast into every player's HUD.
		private void RefreshGlobalEventSystem()
		{
			if (!GlobalDependencyManager.HasInstance ||
				!GlobalDependencyManager.Instance.TryGet(out GameService gameService) ||
				gameService.EventSystem == null)
			{
				return;
			}

			playerEventSystems.RemoveAll((eventSystem) => eventSystem == null);
			gameService.EventSystem.enabled = playerEventSystems.Count == 0;
		}

		/// <summary>
		/// Makes every pair of players enemies, keyed by ID since all players share the same labels.
		/// </summary>
		private void SetPlayersHostile()
		{
			foreach (IAgent a in agents)
			{
				foreach (IAgent b in agents)
				{
					if (a != b && a.Exists() && b.Exists())
					{
						a.Relations.Set(b.Identification.ID, -1f);
					}
				}
			}
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
