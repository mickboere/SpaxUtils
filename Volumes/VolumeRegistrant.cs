using UnityEngine;
using UnityEngine.Rendering;

namespace SpaxUtils
{
	/// <summary>
	/// Place on a <see cref="Volume"/> GameObject to register its profile with the <see cref="VolumeService"/>.
	/// Retrieves the service through <see cref="GlobalDependencyManager"/> since scene objects are not injected.
	/// </summary>
	[RequireComponent(typeof(Volume))]
	public class VolumeRegistrant : MonoBehaviour
	{
		private VolumeService volumeService;
		private VolumeProfile profile;

		private void OnEnable()
		{
			Volume volume = GetComponent<Volume>();
			profile = volume.profile;
			volumeService = GlobalDependencyManager.Instance.Get<VolumeService>();
			volumeService.Register(profile);
		}

		private void OnDisable()
		{
			if (volumeService != null && profile != null)
			{
				volumeService.Unregister(profile);
			}
		}
	}
}
