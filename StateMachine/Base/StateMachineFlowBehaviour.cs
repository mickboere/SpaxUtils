using System.Linq;
using UnityEngine;

namespace SpaxUtils.StateMachines
{
	public class StateMachineFlowBehaviour : MonoBehaviour
	{
		[SerializeField] private FlowGraph flowGraph;
#if UNITY_EDITOR
		[SerializeField, ReadOnly] private string states;
#endif

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

#if UNITY_EDITOR
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
#endif
	}
}
