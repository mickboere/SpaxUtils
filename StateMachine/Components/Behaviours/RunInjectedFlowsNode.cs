using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Node that runs all injected flow-state-machines.
	/// </summary>
	[NodeTint("#6d88a6"), NodeWidth(200)]
	public class RunInjectedFlowsNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;

		private IDependencyManager dependencyManager;
		private IHistory history;
		private FlowGraph[] flowGraphs;

		private List<Flow> subFlows;

		public void InjectDependencies(
			IDependencyManager dependencyManager,
			IHistory history,
			FlowGraph[] flowGraphs)
		{
			this.dependencyManager = dependencyManager;
			this.history = history;
			this.flowGraphs = flowGraphs.Where(g => g.Type == FlowGraph.FlowType.Flow).ToArray();
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			subFlows = new List<Flow>();
			foreach (FlowGraph flowGraph in flowGraphs)
			{
				Flow subFlow = new Flow(flowGraph, dependencyManager, history);
				subFlows.Add(subFlow);
				subFlow.StartFlow();
			}
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			foreach (Flow flow in subFlows)
			{
				flow?.Dispose();
			}
			subFlows.Clear();
		}
	}
}
