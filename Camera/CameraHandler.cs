using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Class that handles a camera entity's references.
	/// </summary>
	public class CameraHandler : EntityComponentMono
	{
		[field: SerializeField] public Camera Cam { get; private set; }
		[field: SerializeField] public ScreenShaker ScreenShaker { get; private set; }

		protected void Awake()
		{
			if (Cam == null) { Cam = GetComponentInChildren<Camera>(); }
			if (ScreenShaker == null) { ScreenShaker = GetComponentInChildren<ScreenShaker>(); }
		}
	}
}
