using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

namespace SpaxUtils
{
	/// <summary>
	/// Global camera service that spawns the persistent main camera rig and tracks per-player camera rigs.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(CameraManager), menuName = "ScriptableObjects/Camera/" + nameof(CameraManager))]
	public class CameraManager : ScriptableObject, IService, ISettingsSupplier
	{
		private const string CAMERA_SETTINGS = "Gameplay/Camera";

		// The persistent camera is a backdrop: base cameras at the default depth (lobby, menus) always draw over it,
		// regardless of it being re-enabled after them when the last player camera goes away.
		private const float PERSISTENT_CAMERA_DEPTH = -1f;

		/// <summary>
		/// Invoked whenever a player camera is registered or unregistered.
		/// </summary>
		public event Action PlayerCamerasChangedEvent;

		/// <summary>
		/// The primary player's camera when one is registered, else the persistent main camera.
		/// </summary>
		public Camera PrimaryCamera => PrimaryHandler != null ? PrimaryHandler.Camera : null;

		/// <summary>
		/// The primary player's brain when one is registered, else the persistent main brain.
		/// </summary>
		public CinemachineBrain PrimaryBrain => PrimaryHandler != null ? PrimaryHandler.Brain : null;

		public Transform Root => instance != null ? instance.transform : null;

		/// <summary>
		/// The persistent main camera rig, which also carries the audio listener.
		/// </summary>
		public MainCameraHandler Handler => handler;

		/// <summary>
		/// Camera rigs owned by players, keyed by player index.
		/// </summary>
		public IReadOnlyDictionary<int, MainCameraHandler> PlayerCameras => playerCameras;

		/// <summary>
		/// Multiplier on all screen shake output.
		/// </summary>
		public float ScreenShake => screenShake.Value;

		/// <summary>
		/// Whether cameras widen their FOV with speed.
		/// </summary>
		public bool SpeedSense => speedSense.Value;

		private MainCameraHandler PrimaryHandler =>
			TryGetPrimaryPlayerCamera(out MainCameraHandler primary, out _) ? primary : handler;

		[SerializeField] private MainCameraHandler mainCameraPrefab;

		[Setting("gameplay.screenShake", CAMERA_SETTINGS, "Screen Shake", 0f, 1f, Format = "0%")]
		[SerializeField] private FloatSetting screenShake = new FloatSetting(1f);
		[Setting("gameplay.speedSense", CAMERA_SETTINGS, "Speed Sense")]
		[SerializeField, Tooltip("FOV widening with movement speed.")]
		private BoolSetting speedSense = new BoolSetting(true);

		private MainCameraHandler handler;
		private GameObject instance;
		private OutputChannels defaultChannelMask;
		private SortedDictionary<int, MainCameraHandler> playerCameras = new SortedDictionary<int, MainCameraHandler>();

		public void InjectDependencies()
		{
			Initialize();
		}

		/// <summary>
		/// The Cinemachine channel owned by player <paramref name="playerIndex"/>; player 0 keeps Default.
		/// </summary>
		public static OutputChannels GetPlayerChannel(int playerIndex)
		{
			return (OutputChannels)(1 << playerIndex);
		}

		/// <summary>
		/// Registers <paramref name="cameraHandler"/> as the camera rig of player <paramref name="playerIndex"/>.
		/// Unregisters automatically when its GameObject is destroyed.
		/// </summary>
		public void RegisterPlayerCamera(int playerIndex, MainCameraHandler cameraHandler)
		{
			if (cameraHandler == null)
			{
				SpaxDebug.Error("Can't register player camera.", $"Camera handler for player {playerIndex} is null.");
				return;
			}

			playerCameras[playerIndex] = cameraHandler;
			if (cameraHandler.Brain != null)
			{
				cameraHandler.Brain.ChannelMask = GetPlayerChannel(playerIndex);
			}
			DestroyNotifier.Get(cameraHandler.gameObject).DestroyedEvent += () => OnPlayerCameraDestroyed(cameraHandler);

			RefreshPersistentRig();
			PlayerCamerasChangedEvent?.Invoke();
		}

		/// <summary>
		/// Removes player <paramref name="playerIndex"/>'s camera rig from the registry.
		/// </summary>
		public void UnregisterPlayerCamera(int playerIndex)
		{
			if (playerCameras.Remove(playerIndex))
			{
				RefreshPersistentRig();
				PlayerCamerasChangedEvent?.Invoke();
			}
		}

		/// <summary>
		/// Returns the squared distance to the closest player camera, or to the main camera when there are none.
		/// </summary>
		public float GetSqrDistanceToClosestCamera(Vector3 point)
		{
			float closest = float.MaxValue;
			foreach (MainCameraHandler playerCamera in playerCameras.Values)
			{
				if (playerCamera != null && playerCamera.Camera != null)
				{
					closest = Mathf.Min(closest, (playerCamera.Camera.transform.position - point).sqrMagnitude);
				}
			}

			if (closest == float.MaxValue && PrimaryCamera)
			{
				closest = (PrimaryCamera.transform.position - point).sqrMagnitude;
			}

			return closest;
		}

		/// <summary>
		/// Returns the distance to the closest player camera, or to the main camera when there are none.
		/// </summary>
		public float GetDistanceToClosestCamera(Vector3 point)
		{
			float sqr = GetSqrDistanceToClosestCamera(point);
			return sqr == float.MaxValue ? float.MaxValue : Mathf.Sqrt(sqr);
		}

		private void Initialize()
		{
			if (handler != null)
			{
				return;
			}

			if (mainCameraPrefab == null)
			{
				SpaxDebug.Error("CameraManager is missing MainCamera prefab reference.", "", this);
				return;
			}

			handler = Instantiate(mainCameraPrefab);
			instance = handler.gameObject;
			DontDestroyOnLoad(instance);

			if (handler.Camera == null)
			{
				SpaxDebug.Error("MainCamera prefab has no Camera in children.", "", instance);
			}
			else
			{
				handler.Camera.depth = PERSISTENT_CAMERA_DEPTH;
			}

			if (handler.Brain == null)
			{
				SpaxDebug.Error("MainCamera prefab has no CinemachineBrain in children.", "", instance);
			}
			else
			{
				defaultChannelMask = handler.Brain.ChannelMask;
			}
		}

		private bool TryGetPrimaryPlayerCamera(out MainCameraHandler primary, out int playerIndex)
		{
			foreach (KeyValuePair<int, MainCameraHandler> playerCamera in playerCameras)
			{
				if (playerCamera.Value != null)
				{
					primary = playerCamera.Value;
					playerIndex = playerCamera.Key;
					return true;
				}
			}

			primary = null;
			playerIndex = -1;
			return false;
		}

		private void OnPlayerCameraDestroyed(MainCameraHandler cameraHandler)
		{
			foreach (KeyValuePair<int, MainCameraHandler> playerCamera in playerCameras)
			{
				if (ReferenceEquals(playerCamera.Value, cameraHandler))
				{
					UnregisterPlayerCamera(playerCamera.Key);
					return;
				}
			}
		}

		// While players own cameras the persistent rig stops rendering, but keeps following the primary
		// player's channel so the audio listener it carries stays with that player.
		private void RefreshPersistentRig()
		{
			if (handler == null)
			{
				return;
			}

			bool hasPlayers = TryGetPrimaryPlayerCamera(out _, out int primaryIndex);
			if (handler.Camera != null)
			{
				handler.Camera.enabled = !hasPlayers;
			}
			if (handler.Brain != null)
			{
				handler.Brain.ChannelMask = hasPlayers ? GetPlayerChannel(primaryIndex) : defaultChannelMask;
			}
		}
	}
}
