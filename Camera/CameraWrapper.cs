using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Class that wraps around the <see cref="Camera"/> functionality.
	/// </summary>
	public class CameraWrapper : EntityComponentMono
	{
		public CompositeFloat FOV { get; private set; }
		public Vector3 Position => transform.position;
		public Quaternion Rotation => transform.rotation;

		[field: SerializeField] public Camera Cam { get; private set; }
		[SerializeField, MinMaxRange(10f, 100f, false)] private Vector2 fovRange = new Vector2(10f, 100f);

		protected void Awake()
		{
			if (Cam == null) { Cam = GetComponentInChildren<Camera>(); }

			FOV = new CompositeFloat(Cam.fieldOfView, null, fovRange.x, fovRange.y);
		}

		protected void LateUpdate()
		{
			Cam.fieldOfView = FOV;
		}
	}
}
