using SpaxUtils.StateMachines;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaxUtils
{
	/// <summary>
	/// Spawns the player entities, lets extra players drop in and out, and cleans them all up once this node exits.
	/// Pressing Start on a gamepad nobody is using joins a new player.
	/// </summary>
	[NodeWidth(250)]
	public class MaintainPlayerEntities : StateComponentNodeBase
	{
		private const int MAX_PLAYERS = 2;
		private const float JOIN_SPAWN_RADIUS = 3f;

		[SerializeField] private PlayerConfig playerConfig;
		[SerializeField, ConstDropdown(typeof(ISpawnpointIdentifiers))] private string defaultSpawnpoint;
		[SerializeField] private AgentSpawnData spawnData;

		private IDependencyManager dependencyManager;
		private IEntityCollection entityCollection;
		private PlayerAgentService playerAgentService;
		private PlayerInputService playerInputService;
		private CallbackService callbackService;
		private TimeService timeService;
		private CutsceneService cutsceneService;
		private PlayerDeviceRouter deviceRouter;

		private Dictionary<int, List<GameObject>> instances = new Dictionary<int, List<GameObject>>();
		private Gamepad pendingJoin;
		private HashSet<int> pendingLeaves = new HashSet<int>();

		public void InjectDependencies(IDependencyManager dependencyManager, IEntityCollection entityCollection,
			PlayerAgentService playerAgentService, PlayerInputService playerInputService, CallbackService callbackService,
			TimeService timeService, CutsceneService cutsceneService, PlayerDeviceRouter deviceRouter)
		{
			this.dependencyManager = dependencyManager;
			this.entityCollection = entityCollection;
			this.playerAgentService = playerAgentService;
			this.playerInputService = playerInputService;
			this.callbackService = callbackService;
			this.timeService = timeService;
			this.cutsceneService = cutsceneService;
			this.deviceRouter = deviceRouter;
		}

		public override void OnEnteringState(ITransition transition)
		{
			base.OnEnteringState(transition);

			Spawn(0);

			// Joined players keep their paired input across level loads, so they come back with player one.
			foreach (int playerIndex in playerInputService.Wrappers.Keys.Where((i) => i > 0 && i < MAX_PLAYERS).ToList())
			{
				if (Spawn(playerIndex) == null)
				{
					playerInputService.Remove(playerIndex);
				}
			}

			deviceRouter.JoinRequestedEvent += OnJoinRequested;
			deviceRouter.DeviceLostEvent += OnLeaveRequested;
			deviceRouter.Start(this, CanJoin);
			playerAgentService.LeaveRequestedEvent += OnLeaveRequested;
			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			callbackService.UnsubscribeUpdates(this);
			playerAgentService.LeaveRequestedEvent -= OnLeaveRequested;
			deviceRouter.JoinRequestedEvent -= OnJoinRequested;
			deviceRouter.DeviceLostEvent -= OnLeaveRequested;
			deviceRouter.Stop(this);
			pendingJoin = null;
			pendingLeaves.Clear();

			foreach (int playerIndex in instances.Keys.ToList())
			{
				DestroyInstances(playerIndex);
			}

			// Joined players' wrappers outlive their HUD; drop requests the destroyed HUD may have left behind.
			foreach (KeyValuePair<int, PlayerInputWrapper> wrapper in playerInputService.Wrappers)
			{
				if (wrapper.Key > 0 && wrapper.Value != null)
				{
					wrapper.Value.ResetInputState();
				}
			}
		}

		private void OnUpdate(float delta)
		{
			foreach (int playerIndex in pendingLeaves.ToList())
			{
				Leave(playerIndex);
			}
			pendingLeaves.Clear();

			if (pendingJoin != null)
			{
				Gamepad gamepad = pendingJoin;
				pendingJoin = null;
				if (gamepad.added && CanJoin())
				{
					Join(gamepad);
				}
			}
		}

		private IAgent Spawn(int playerIndex)
		{
			if (!TryGetSpawnPose(playerIndex, out Vector3 position, out Quaternion rotation))
			{
				SpaxDebug.Error("Was unable to find spawnpoint.", defaultSpawnpoint);
				return null;
			}

			List<GameObject> playerInstances = new List<GameObject>();
			instances[playerIndex] = playerInstances;

			IAgent player = playerAgentService.SpawnPlayer(
				dependencyManager, playerConfig, spawnData, position, rotation, playerIndex, playerInstances);
			if (player == null)
			{
				DestroyInstances(playerIndex);
				return null;
			}

			player.Brain.TryTransition(AgentStateIdentifiers.ACTIVE);
			return player;
		}

		private bool TryGetSpawnPose(int playerIndex, out Vector3 position, out Quaternion rotation)
		{
			// Joined players appear at a walkable spot around player one, facing them; else at the default spawnpoint.
			IAgent main = playerAgentService.PlayerAgent;
			if (playerIndex > 0 && main.Exists())
			{
				Vector3 center = main.Transform.position;
				AgentNavigationHandler navigation = main.GetEntityComponent<AgentNavigationHandler>();
				if (navigation != null && navigation.TrySampleReachablePoint(center, JOIN_SPAWN_RADIUS, out position))
				{
					Vector3 toMain = Vector3.ProjectOnPlane(center - position, Vector3.up);
					rotation = toMain.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toMain) : main.Transform.rotation;
					return true;
				}
			}

			Transform spawnpoint = entityCollection.Get<IEntity>((entity) => entity.ID == defaultSpawnpoint)
				.FirstOrDefault()?.GameObject.transform;
			position = spawnpoint != null ? spawnpoint.position : Vector3.zero;
			rotation = spawnpoint != null ? spawnpoint.rotation : Quaternion.identity;
			return spawnpoint != null;
		}

		private void Join(Gamepad gamepad)
		{
			int playerIndex = GetFreePlayerIndex();
			if (playerIndex < 0)
			{
				return;
			}

			playerInputService.CreatePaired(playerIndex, playerConfig.InputActionAsset, ControlSchemes.GAMEPAD, gamepad);
			if (Spawn(playerIndex) == null)
			{
				playerInputService.Remove(playerIndex);
			}
		}

		private void Leave(int playerIndex)
		{
			// Player one leaving is quitting, which the menus already handle.
			if (playerIndex <= 0)
			{
				return;
			}

			// Keeps their progress for a rejoin this session; the disk only changes on an explicit save.
			IAgent agent = playerIndex < playerAgentService.Agents.Count ? playerAgentService.Agents[playerIndex] : null;
			if (agent.Exists())
			{
				agent.SaveData();
			}

			DestroyInstances(playerIndex);
			playerInputService.Remove(playerIndex);
		}

		private void DestroyInstances(int playerIndex)
		{
			if (instances.TryGetValue(playerIndex, out List<GameObject> playerInstances))
			{
				foreach (GameObject instance in playerInstances)
				{
					Destroy(instance);
				}
				instances.Remove(playerIndex);
			}
		}

		private bool CanJoin()
		{
			return GetFreePlayerIndex() > 0 && !timeService.Paused && !cutsceneService.Playing;
		}

		private int GetFreePlayerIndex()
		{
			for (int i = 1; i < MAX_PLAYERS; i++)
			{
				if (!playerInputService.TryGet(i, out _))
				{
					return i;
				}
			}
			return -1;
		}

		private void OnJoinRequested(Gamepad gamepad)
		{
			// Deferred: spawning inside input event processing would let the joining press reach the new player.
			if (pendingJoin == null)
			{
				pendingJoin = gamepad;
			}
		}

		private void OnLeaveRequested(int playerIndex)
		{
			// Deferred: requests arrive mid UI or input event (the leaving player's own menu, an unplugged pad).
			pendingLeaves.Add(playerIndex);
		}
	}
}
