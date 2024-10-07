using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils.StateMachines
{
	public class FlowGraph : StateMachineGraph
	{
		public enum FlowType
		{
			Flow,
			Dialogue
		}

		public FlowType Type => type;

		[SerializeField] private FlowType type;

		public List<FlowStateNode> GetStartStates()
		{
			List<FlowStateNode> startNodes = GetNodesOfType<FlowStateNode>().Where(n => n.StartState).ToList();
			if (startNodes.Count == 0)
			{
				SpaxDebug.Error("Could not find a starting state.");
				return null;
			}

			return startNodes;
		}
	}
}
