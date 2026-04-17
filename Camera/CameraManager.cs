using UnityEngine;
using Unity.Cinemachine;

namespace SpaxUtils
{
	/// <summary>
	/// Global camera service that spawns and tracks the persistent main camera rig.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(CameraManager), menuName = "ScriptableObjects/Camera/" + nameof(CameraManager))]
	public class CameraManager : ScriptableObject, IService
	{
		public Camera MainCamera => handler != null ? handler.Camera : null;
		public CinemachineBrain Brain => handler != null ? handler.Brain : null;
		public Transform Root => instance != null ? instance.transform : null;
		public MainCameraHandler Handler => handler;

		[SerializeField] private MainCameraHandler mainCameraPrefab;

		private MainCameraHandler handler;
		private GameObject instance;

		public void InjectDependencies()
		{
			Initialize();
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

			if (handler.Brain == null)
			{
				SpaxDebug.Error("MainCamera prefab has no CinemachineBrain in children.", "", instance);
			}
		}

		/// <summary>
		/// Returns the squared distance to the main camera.
		/// </summary>
		public float GetSqrDistanceToMainCamera(Vector3 point)
		{
			if (!MainCamera)
			{
				return float.MaxValue;
			}
			return (MainCamera.transform.position - point).sqrMagnitude;
		}

		/// <summary>
		/// Returns the distance to the main camera.
		/// </summary>
		public float GetDistanceToMainCamera(Vector3 point)
		{
			if (!MainCamera)
			{
				return float.MaxValue;
			}
			return (MainCamera.transform.position - point).magnitude;
		}
	}
}
