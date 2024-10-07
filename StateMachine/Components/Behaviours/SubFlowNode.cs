using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Node that runs a another flow state machine.
	/// </summary>
	[NodeTint("#6d88a6"), NodeWidth(200)]
	public class SubFlowNode : StateMachineNodeBase, IRule
	{
		public override string UserFacingName => flowGraph != null ? flowGraph.name : "Empty";

		public bool Valid => !subFlow.Running;
		public float Validity => 1f;

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.Rule exitedFlowRule;
		[SerializeField] private FlowGraph flowGraph;

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

			subFlow = new Flow(flowGraph, dependencyManager, history);
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