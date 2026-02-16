using UnityEngine;
using Unity.Cinemachine;

namespace SpaxUtils
{
	/// <summary>
	/// Adds a rotation/position correction at the end of the Cinemachine pipeline.
	/// This is the clean place to inject procedural shake without touching transforms.
	/// </summary>
	public class CinemachineShakeExtension : CinemachineExtension, IDependency
	{
		private Vector3 rotationEuler;
		private Vector3 positionLocal;

		public void SetRotationEuler(Vector3 euler)
		{
			rotationEuler = euler;
		}

		public void SetPositionLocal(Vector3 localOffset)
		{
			positionLocal = localOffset;
		}

		public void Clear()
		{
			rotationEuler = Vector3.zero;
			positionLocal = Vector3.zero;
		}

		protected override void PostPipelineStageCallback(
			CinemachineVirtualCameraBase vcam,
			CinemachineCore.Stage stage,
			ref CameraState state,
			float deltaTime)
		{
			if (stage != CinemachineCore.Stage.Finalize)
			{
				return;
			}

			if (positionLocal != Vector3.zero)
			{
				state.PositionCorrection += state.RawOrientation * positionLocal;
			}

			if (rotationEuler != Vector3.zero)
			{
				state.OrientationCorrection = state.OrientationCorrection * Quaternion.Euler(rotationEuler);
			}
		}
	}
}
