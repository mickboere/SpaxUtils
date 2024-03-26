using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XNode;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// <see cref="NodeGraph"/> implementation for state machine assets.
	/// </summary>
	[CreateAssetMenu(fileName = "StateMachineGraph", menuName = "StateMachine/StateMachineGraph")]
	public class StateMachineGraph : NodeGraph
	{
		public List<FlowStateNode> GetStartStates()
		{
			// Collect all FlowStateNodes and remove nodes that aren't starting states.
			List<FlowStateNode> startNodes = GetNodesOfType<FlowStateNode>();
			for (int i = 0; i < startNodes.Count; i++)
			{
				if (!startNodes[i].StartState)
				{
					startNodes.RemoveAt(i);
					i--;
				}
			}

			if (startNodes.Count() == 0)
			{
				SpaxDebug.Error("Could not find a starting state.");
				return null;
			}

			return startNodes;
		}

		public List<T> GetNodesOfType<T>()
		{
			List<T> ofType = new List<T>();
			foreach (Node node in nodes)
			{
				if (node is T cast)
				{
					ofType.Add(cast);
				}
			}
			return ofType;
		}
	}
}
