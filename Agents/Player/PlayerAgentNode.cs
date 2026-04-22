using SpaxUtils.StateMachines;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Node that will mark an agent as currently being controlled by a player.
	/// </summary>
	public class PlayerAgentNode : StateMachineNodeBase
	{
		[SerializeField, NodeInput] protected Connections.StateComponent inConnection;

		private IAgent agent;
		private PlayerInputWrapper playerInputWrapper;
		private PlayerAgentService playerAgentService;

		public void InjectDependencies(
			IAgent agent,
			PlayerInputWrapper playerInputWrapper,
			PlayerAgentService playerAgentService)
		{
			this.agent = agent;
			this.playerInputWrapper = playerInputWrapper;
			this.playerAgentService = playerAgentService;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			playerAgentService.MarkPlayerAgent(agent, playerInputWrapper.PlayerIndex);
		}

		public override void OnExitingState(ITransition transition)
		{
			base.OnExitingState(transition);
			playerAgentService.DismissPlayerAgent(agent);
		}
	}
}
