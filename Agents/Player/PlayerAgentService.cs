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
		public IReadOnlyList<IAgent> Agents => _agents;
		private List<IAgent> _agents = new List<IAgent>();

		/// <summary>
		/// The <see cref="IAgent"/> of player one.
		/// </summary>
		public IAgent PlayerAgent => _agents.Count > 0 ? _agents[0] : null;

		private RuntimeDataService runtimeDataService;

		public PlayerAgentService(RuntimeDataService runtimeDataService)
		{
			this.runtimeDataService = runtimeDataService;
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
		public void MarkPlayerAgent(IAgent agent, int playerIndex)
		{
			if (_agents.Count <= playerIndex)
			{
				_agents.Add(agent);
			}
			else
			{
				_agents[playerIndex] = agent;
			}

			// Load player collection.
			List<string> playerCollection = new List<string>();
			if (runtimeDataService.CurrentProfile.ContainsEntry(ID_PLAYER_COLLECTION))
			{
				playerCollection = runtimeDataService.CurrentProfile.GetValue<List<string>>(ID_PLAYER_COLLECTION);
			}

			if (playerIndex < playerCollection.Count)
			{
				// Overwrite entity at player index.
				playerCollection[playerIndex] = agent.Identification.ID;
				runtimeDataService.CurrentProfile.SetValue(ID_PLAYER_COLLECTION, playerCollection);
			}
			else
			{
				// Expand player collection.
				playerCollection.Add(agent.Identification.ID);
				runtimeDataService.CurrentProfile.SetValue(ID_PLAYER_COLLECTION, playerCollection);
			}

			PlayerRegisteredEvent?.Invoke(agent);
		}

		/// <summary>
		/// Remove an agent from the tracked player agents list.
		/// </summary>
		public void DismissPlayerAgent(int index)
		{
			if (_agents.Count > index)
			{
				IAgent agent = null;

				if (_agents[index] != null)
				{
					agent = _agents[index];
				}

				_agents.RemoveAt(index);

				if (agent != null)
				{
					PlayerDeregisteredEvent?.Invoke(agent);
				}
			}
		}

		/// <summary>
		/// Remove an agent from the tracked player agents list.
		/// </summary>
		public void DismissPlayerAgent(IAgent agent)
		{
			_agents.Remove(agent);
			PlayerDeregisteredEvent?.Invoke(agent);
		}

		/// <summary>
		/// Spawns a new player agent.
		/// </summary>
		public IAgent SpawnPlayer(IDependencyManager dependencyManager, PlayerConfig config, AgentSpawnData spawnData, Transform spawnpoint,
			out List<GameObject> instances, Camera inputCamOverride = null, bool activate = true)
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
			GameObject cameraInstance = null;
			Camera cameraComponent = null;
			if (config.CameraPrefab != null)
			{
				cameraInstance = DependencyUtils.InstantiateDeactivated(config.CameraPrefab, spawnpoint.position, spawnpoint.rotation);
				instances.Add(cameraInstance);

				cameraComponent = cameraInstance.GetComponentInChildren<Camera>();
				cameraDependencies = new DependencyManager(playerDependencies, "PlayerCamera");
				playerDependencies.Bind(EntityLabels.CAMERA, cameraComponent);
				playerDependencies.Bind(cameraComponent);
			}
			UIRoot hudInstance = null;
			if (config.UIPrefab != null)
			{
				hudInstance = DependencyUtils.InstantiateDeactivated(config.UIPrefab.gameObject).GetComponent<UIRoot>();
				instances.Add(hudInstance.gameObject);
			}

			// Create player input
			Camera inputCam = inputCamOverride != null ? inputCamOverride : cameraComponent != null ? cameraComponent : hudInstance != null ? hudInstance.Camera : null;
			PlayerInputWrapper playerInputWrapper = PlayerInputWrapper.Create(config.InputActionAsset, playerDependencies, inputCam);
			instances.Add(playerInputWrapper.gameObject);

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
			}

			// Create player setup with loaded ID, if any.
			IIdentification identification =
				entityData == null ?
					config.AgentSetup.Identification :
					new Identification(entityData.ID, config.AgentSetup.Identification.Name, config.AgentSetup.Identification.Labels, null);
			AgentSetup setup = new AgentSetup(config.AgentSetup, identification);

			// Create player agent.
			Agent playerAgent = spawnData.Spawn(setup, playerDependencies, spawnpoint.position, spawnpoint.rotation, activate);
			instances.Add(playerAgent.gameObject);

			// Set up player camera.
			if (cameraInstance != null)
			{
				string camName = $"PLAYER_CAMERA_{playerInputWrapper.PlayerIndex}";
				var cameraIdentification = new Identification(camName, camName, new List<string>() { EntityLabels.CAMERA }, cameraInstance.GetComponent<IEntity>());
				cameraDependencies.Bind(cameraIdentification);
				DependencyUtils.BindMonoBehaviours(cameraInstance, cameraDependencies, includeChildren: true);
				DependencyUtils.Inject(cameraInstance, cameraDependencies, includeChildren: true, bindComponents: false);
				cameraInstance.SetActive(true);
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

			return playerAgent;
		}

		/// <summary>
		/// Returns whether the player with index <paramref name="playerIndex"/> is currently alive, whether actively or in its runtime data.
		/// </summary>
		public bool IsAlive(int playerIndex = 0)
		{
			return
				(_agents.Count > playerIndex &&
				_agents[playerIndex].RuntimeData.GetValue(EntityDataIdentifiers.ALIVE, false))
				||
				(TryRetrievePlayerEntityData(playerIndex, out RuntimeDataCollection playerData) &&
				playerData.GetValue(EntityDataIdentifiers.ALIVE, false));
		}

		/// <summary>
		/// Returns the squared distance to the closest player agent.
		/// </summary>
		public float GetSqrDistanceToClosestPlayer(Vector3 point, out IAgent closest)
		{
			closest = null;
			float closestDistance = float.MaxValue;
			foreach (IAgent player in _agents)
			{
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
	}
}
