using System.Collections;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Makes sure the <see cref="GameService"/> exists as soon as this object awakes.
	/// </summary>
	[DefaultExecutionOrder(-10000)]
	public class GameInitializer : MonoBehaviour
	{
		protected void Awake()
		{
			GlobalDependencyManager.Instance.Get<GameService>();
		}
	}
}
