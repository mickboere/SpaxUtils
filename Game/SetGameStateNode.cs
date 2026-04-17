using UnityEngine;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	public class SetGameStateNode : StateComponentNodeBase
	{
		[SerializeField, ConstDropdown(typeof(IStateIdentifiers), showAdress: true)] private string gameState;
		[SerializeField] private float duration = -1f;

		private GameService gameService;

		public void InjectDependencies(GameService gameService)
		{
			this.gameService = gameService;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			gameService.SwitchState(gameState, duration);
		}
	}
}
