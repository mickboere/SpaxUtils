using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XNode;

namespace SpaxUtils.StateMachine
{
	/// <summary>
	/// <see cref="NodeGraph"/> implementation for state machine assets.
	/// </summary>
	[CreateAssetMenu(fileName = "StateMachineGraph", menuName = "StateMachine/StateMachineGraph")]
	public class StateMachineGraph : NodeGraph
	{
		public List<FlowStateNode> GetStartStates()
		{
			List<FlowStateNode> startNodes = nodes.Where((n) => n is FlowStateNode flowState && flowState.StartState).Cast<FlowStateNode>().ToList();

			if (startNodes.Count() == 0)
			{
				SpaxDebug.Error("Could not find a starting state.");
				return null;
			}

			return startNodes;
		}
	}
}
