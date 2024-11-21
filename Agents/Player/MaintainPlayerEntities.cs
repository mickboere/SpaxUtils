using SpaxUtils.StateMachines;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace SpaxUtils
{
	/// <summary>
	/// Spawns the player entities and cleans them up once this node is exits.
	/// </summary>
	[NodeWidth(250)]
	public class MaintainPlayerEntities : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField] private AgentSetupAsset playerSetup;
		[SerializeField] private GameObject playerCameraPrefab;
		[SerializeField] private InputActionAsset inputActionAsset;
		[SerializeField] private GameObject playerHudPrefab;
		[SerializeField, ConstDropdown(typeof(ISpawnpointIdentifiers))] private string defaultSpawnpoint;
		[SerializeField] private AgentSpawnData spawnData;

		private IDependencyManager dependencyManager;
		private IEntityCollection entityCollection;
		private PlayerAgentService playerAgentService;
		private RuntimeDataService runtimeDataService;

		private List<GameObject> instances = new List<GameObject>();

		public void InjectDependencies(IDependencyManager dependencyManager, IEntityCollection entityCollection,
			PlayerAgentService playerAgentService, RuntimeDataService runtimeDataService)
		{
			this.dependencyManager = dependencyManager;
			this.entityCollection = entityCollection;
			this.playerAgentService = playerAgentService;
			this.runtimeDataService = runtimeDataService;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			SpawnPlayer();
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			foreach (GameObject instance in instances)
			{
				Destroy(instance);
			}
			instances.Clear();
		}

		private void SpawnPlayer()
		{
			List<IEntity> foundSpawnpoints = entityCollection.Get<IEntity>((entity) => entity.ID == defaultSpawnpoint);
			Transform spawnpoint = foundSpawnpoints.FirstOrDefault()?.GameObject.transform;
			if (spawnpoint != null)
			{
				// Ensure all the required assets are present.
				if (playerSetup == null ||
					playerCameraPrefab == null ||
					inputActionAsset == null)
				{
					SpaxDebug.Error("One or more required player assets are missing.");
					return;
				}

				// Create deactivated instances.
				GameObject cameraInstance = DependencyUtils.InstantiateDeactivated(playerCameraPrefab, spawnpoint.position, spawnpoint.rotation);
				instances.Add(cameraInstance);
				GameObject hudInstance = playerHudPrefab != null ? DependencyUtils.InstantiateDeactivated(playerHudPrefab) : null;
				instances.Add(hudInstance);

				// Create player input
				Camera cameraComponent = cameraInstance.GetComponentInChildren<Camera>();
				PlayerInputWrapper playerInputWrapper = PlayerInputWrapper.Create(inputActionAsset, dependencyManager, cameraComponent);
				instances.Add(playerInputWrapper.gameObject);

				// Create dependency managers for entities.
				DependencyManager playerDependencies = new DependencyManager(dependencyManager, "Player");
				DependencyManager cameraDependencies = new DependencyManager(playerDependencies, "PlayerCamera");

				// Bind necessary data to dependency managers.
				playerDependencies.Bind(EntityLabels.CAMERA, cameraComponent);
				playerDependencies.Bind(cameraComponent);
				RuntimeDataCollection entityData;
				if (playerAgentService.TryRetrievePlayerEntityData(playerInputWrapper.PlayerIndex, out entityData))
				{
					playerDependencies.Bind(entityData);
				}
				else if (playerInputWrapper.PlayerIndex == 0)
				{
					// The main player character is being spawned for the first time, give it the correct data to initialize.
					entityData = new RuntimeDataCollection(Guid.NewGuid().ToString(), new List<RuntimeDataEntry>()
					{
						new RuntimeDataEntry(EntityDataIdentifiers.NAME, runtimeDataService.CurrentProfile.ID)
					});
					playerDependencies.Bind(entityData);
				}

				// Create player setup with loaded ID, if any.
				IIdentification identification =
					entityData == null ?
						playerSetup.Identification :
						new Identification(entityData.ID, playerSetup.Identification.Name, playerSetup.Identification.Labels, null);
				AgentSetup setup = new AgentSetup(playerSetup, identification);

				// Create player agent.
				Agent playerAgent = spawnData.Spawn(setup, playerDependencies, spawnpoint.position, spawnpoint.rotation);
				instances.Add(playerAgent.gameObject);

				// Set up player camera.
				string camName = $"PLAYER_CAMERA_{playerInputWrapper.PlayerIndex}";
				var cameraIdentification = new Identification(camName, camName, new List<string>() { EntityLabels.CAMERA }, cameraInstance.GetComponent<IEntity>());
				cameraDependencies.Bind(cameraIdentification);
				DependencyUtils.BindMonoBehaviours(cameraInstance, cameraDependencies, includeChildren: true);
				DependencyUtils.Inject(cameraInstance, cameraDependencies, includeChildren: true, bindComponents: false);
				cameraInstance.SetActive(true);

				// Set up HUD.
				if (hudInstance != null)
				{
					// Create new DependencyManager from the camera's DependencyManager because HUD is tied to the camera anyways.
					DependencyManager hudDependencies = new DependencyManager(cameraDependencies, "PlayerHUD");
					DependencyUtils.BindMonoBehaviours(hudInstance, hudDependencies, includeChildren: true);
					DependencyUtils.Inject(hudInstance, hudDependencies, includeChildren: true, bindComponents: false);
					hudInstance.SetActive(true);

					Camera uiCamera = hudInstance.GetComponentInChildren<Camera>();
					if (uiCamera != null)
					{
						// Add UI camera to main camera stack.
						var cameraData = cameraComponent.GetUniversalAdditionalCameraData();
						cameraData.cameraStack.Add(uiCamera);
					}
				}

				SpaxDebug.Notify("Spawned player entites.");
			}
			else
			{
				SpaxDebug.Error("Was unable to find spawnpoint.", this.defaultSpawnpoint);
			}
		}
	}
}
