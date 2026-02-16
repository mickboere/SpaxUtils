using UnityEngine;
using Unity.Cinemachine;

namespace SpaxUtils
{
	/// <summary>
	/// Class that wraps around the <see cref="CinemachineCamera"/>.
	/// </summary>
	public class CineCameraWrapper : EntityComponentMono
	{
		public CompositeFloat FOV { get; private set; }
		public Vector3 Position { get { return transform.position; } set { transform.position = value; } }
		public Quaternion Rotation { get { return transform.rotation; } set { transform.rotation = value; } }

		[field: SerializeField] public CinemachineCamera CineCam { get; private set; }
		[SerializeField, MinMaxRange(10f, 100f, false)] private Vector2 fovRange = new Vector2(10f, 100f);

		protected void Awake()
		{
			if (CineCam == null)
			{
				CineCam = GetComponentInChildren<CinemachineCamera>();
			}

			if (CineCam != null)
			{
				FOV = new CompositeFloat(CineCam.Lens.FieldOfView, null, fovRange.x, fovRange.y);
			}
			else
			{
				FOV = new CompositeFloat(60f, null, fovRange.x, fovRange.y);
				SpaxDebug.Error("Missing CinemachineCamera.", "", gameObject);
			}
		}

		protected void LateUpdate()
		{
			if (CineCam == null)
			{
				return;
			}

			var lens = CineCam.Lens;
			lens.FieldOfView = FOV;
			CineCam.Lens = lens;
		}
	}
}
