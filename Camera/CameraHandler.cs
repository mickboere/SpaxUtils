using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Class that handles a camera entity's references.
	/// </summary>
	public class CameraHandler : EntityComponentMono
	{
		[field: SerializeField] public Camera Cam { get; private set; }


	}
}
