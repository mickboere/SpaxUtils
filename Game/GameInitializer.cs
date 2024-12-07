using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// MonoBehaviour that ensures all the game's required services are running.
	/// </summary>
	[DefaultExecutionOrder(-100000)]
	public class GameInitializer : MonoBehaviour
	{
#if UNITY_EDITOR
		[SerializeField, ReadOnly] private string gameState;
#endif

		private GameService gameService;

		protected void Awake()
		{
			gameService = GlobalDependencyManager.Instance.Get<GameService>();
		}

#if UNITY_EDITOR
		protected void Update()
		{
			gameState = "[" + string.Join(", ", gameService.Brain.StateHierarchy.Select(s => s.ID).ToList()) + "]";
		}
#endif
	}
}
