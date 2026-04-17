using System.Linq;
using UnityEngine;

namespace SpaxUtils.StateMachines
{
	public class StateMachineFlowBehaviour : MonoBehaviour
	{
		[SerializeField] private FlowGraph flowGraph;
#if UNITY_EDITOR
		[SerializeField, TextArea, ReadOnly] private string states;
#endif

		private IDependencyManager dependencies;
		private Flow flow;
		private StateMachineHistory history;

		public void InjectDependencies(IDependencyManager dependencies)
		{
			this.dependencies = dependencies;
			history = new StateMachineHistory();
		}

		protected void Awake()
		{
			flow = new Flow(flowGraph, dependencies, history);
		}

		protected void OnEnable()
		{
			flow.StartFlow();
		}

		protected void OnDisable()
		{
			flow.StopFlow();
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
				states = "[(" + string.Join(")]\n[(", flow.Layers.Select(l => string.Join("), (", l.StateHierarchy.Select(s => s.ID).ToList())).ToList()) + ")]";
			}
		}
#endif
	}
}
