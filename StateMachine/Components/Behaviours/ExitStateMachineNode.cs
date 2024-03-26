using UnityEngine;

namespace SpaxUtils.StateMachines
{
	[NodeTint("#FD383D"), NodeWidth(130)]
	public class ExitStateMachineNode : StateMachineNodeBase
	{
		public override string UserFacingName => "Exit";

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;

		private Flow flow;

		public void InjectDependencies(Flow flow)
		{
			this.flow = flow;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			flow.StopFlow();
		}
	}
}