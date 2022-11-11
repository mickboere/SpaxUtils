using SpaxUtils.StateMachine;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Node that will mark an agent as currently being controlled by a player.
	/// </summary>
	public class PlayerAgentNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;

		private IEntity entity;
		private PlayerInputWrapper playerInputWrapper;
		private PlayerAgentService playerAgentService;

		public void InjectDependencies(
			IEntity entity,
			PlayerInputWrapper playerInputWrapper,
			PlayerAgentService playerAgentService)
		{
			this.entity = entity;
			this.playerInputWrapper = playerInputWrapper;
			this.playerAgentService = playerAgentService;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			playerAgentService.MarkPlayerEntity(entity, playerInputWrapper.PlayerIndex);
		}
	}
}
