using UnityEngine;

namespace SpaxUtils.StateMachine
{
	[NodeWidth(140), NodeTint("#a3334b")]
	public class BackToStartNode : StateMachineNodeBase
	{
		public override int ExecutionOrder => 999;

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField] private bool immediate;

		private FlowLayer flowLayer;

		public void InjectDependencies(FlowLayer flowLayer)
		{
			this.flowLayer = flowLayer;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			flowLayer.Start(immediate);
		}
	}
}
