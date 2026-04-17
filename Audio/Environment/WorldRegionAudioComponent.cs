using UnityEngine;

namespace SpaxUtils
{
	public class WorldRegionAudioComponent : MonoBehaviour
	{
		public IEnvironmentAudioSettings Settings => asset == null ? custom : asset;

		[SerializeField] private EnvironmentAudioSettingsAsset asset;
		[SerializeField] private EnvironmentAudioSettings custom;
	}
}
