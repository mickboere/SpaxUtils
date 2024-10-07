using UnityEngine;

namespace SpaxUtils.StateMachines
{
	public class StateMachineFlowBehaviour : MonoBehaviour
	{
		[SerializeField] private FlowGraph flowGraph;

		private IDependencyManager dependencies;
		private Flow flow;
		private StateMachineHistory history;

		public void InjectDependencies(IDependencyManager dependencies)
		{
			this.dependencies = dependencies;
			history = new StateMachineHistory();
		}

		protected void Start()
		{
			flow = new Flow(flowGraph, dependencies, history);
			flow.StartFlow();
		}
	}
}
