using UnityEngine;

namespace SpaxUtils
{
	public class AudioInitializer : MonoBehaviour
	{
		protected void Start()
		{
			GlobalDependencyManager.Instance.Get<AudioManager>();
			GlobalDependencyManager.Instance.Get<EnvironmentAudioManager>().Override(null);
		}
	}
}
