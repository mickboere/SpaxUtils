using System.Linq;
using UnityEngine;

namespace SpaxUtils.StateMachines
{
	public class StateMachineFlowBehaviour : MonoBehaviour
	{
		[SerializeField] private FlowGraph flowGraph;
		[SerializeField, ReadOnly] private string states;

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

		protected void OnDestroy()
		{
			flow?.Dispose();
		}

		protected void Update()
		{
			if (flow == null)
			{
				states = "NULL";
			}
			else
			{
				states = string.Join(", ", flow.Layers.Select(l => l.HeadState).ToList());
			}
		}
	}
}
