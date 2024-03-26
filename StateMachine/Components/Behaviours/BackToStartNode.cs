using UnityEngine;

namespace SpaxUtils.StateMachines
{
	[NodeWidth(140), NodeTint("#a3334b")]
	public class BackToStartNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField] private bool immediate;

		private StateMachine stateMachine;

		public void InjectDependencies(StateMachine stateMachine)
		{
			this.stateMachine = stateMachine;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			stateMachine.TransitionToDefaultState();
		}
	}
}
