using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// MonoBehaviour that ensures all the game's required services are running.
	/// </summary>
	[DefaultExecutionOrder(-9999)]
	public class GameInitializer : MonoBehaviour
	{
		protected void Awake()
		{
			GlobalDependencyManager.Instance.Get<GameService>();
		}
	}
}
