using UnityEngine;

namespace SpaxUtils.StateMachines
{
	public class StateMachineFlowBehaviour : MonoBehaviour
	{
		[SerializeField] private StateMachineGraph stateMachine;

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
			flow = new Flow(stateMachine, dependencies, history);
			flow.StartFlow();
		}
	}
}
