using SpaxUtils.StateMachines;
using System.Collections.Generic;

namespace SpaxUtils
{
	public class GameService : IService
	{
		public GameData GameData { get; }
		public UIRoot UIRoot { get; }

		public GameService(GameData gameData, IDependencyManager dependencyManager)
		{
			GameData = gameData;
			UIRoot = DependencyUtils.InstantiateAndInject(GameData.UIRoot.gameObject, dependencyManager).GetComponent<UIRoot>();
		}
	}
}
