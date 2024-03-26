using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Node that runs a sub-state-machine.
	/// </summary>
	[NodeTint("#6d88a6"), NodeWidth(200)]
	public class SubStateMachineNode : StateMachineNodeBase, IRule
	{
		public override string UserFacingName => flow != null ? flow.name : "Empty";

		public bool Valid => !subFlow.Running;
		public float Validity => 1f;

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.Rule exitedFlowRule;
		[SerializeField] private StateMachineGraph flow;

		private IDependencyManager dependencyManager;
		private IHistory history;
		private Flow mainFlow;

		private Flow subFlow;

		public void InjectDependencies(
			IDependencyManager dependencyManager,
			IHistory history,
			Flow mainFlow)
		{
			this.dependencyManager = dependencyManager;
			this.history = history;
			this.mainFlow = mainFlow;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			subFlow = new Flow(flow, dependencyManager, history);
			subFlow.StartFlow();
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			if (subFlow.Running)
			{
				subFlow.Dispose();
				subFlow = null;
			}
		}
	}
}