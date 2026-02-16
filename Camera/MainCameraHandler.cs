using UnityEngine;
using Unity.Cinemachine;

namespace SpaxUtils
{
	/// <summary>
	/// Prefab/instance handle for the persistent main camera rig.
	/// Must live on the root of the persistent camera prefab.
	/// </summary>
	public class MainCameraHandler : MonoBehaviour
	{
		public Camera Camera => cameraRef != null ? cameraRef : GetCamera();
		public CinemachineBrain Brain => brainRef != null ? brainRef : GetBrain();

		[SerializeField] private Camera cameraRef;
		[SerializeField] private CinemachineBrain brainRef;

		private Camera GetCamera()
		{
			cameraRef = GetComponentInChildren<Camera>(true);
			return cameraRef;
		}

		private CinemachineBrain GetBrain()
		{
			brainRef = GetComponentInChildren<CinemachineBrain>(true);
			return brainRef;
		}
	}
}
