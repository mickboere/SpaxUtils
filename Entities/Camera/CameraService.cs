using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Service that keeps track of the currently active camera.
	/// </summary>
	public class CameraService : IService
	{
		public Camera MainCamera { get; private set; }

		public void SetMainCamera(Camera camera)
		{
			MainCamera = camera;
		}

		/// <summary>
		/// Returns the squared distance to the closest player agent.
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
		/// Returns the distance to the closest player agent.
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
