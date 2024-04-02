using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XNode;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// StateMachine specific <see cref="Node"/> extensions.
	/// </summary>
	public static class XNodeExtensions
	{
		/// <summary>
		/// Gets all of the nodes connected to the specified input port.
		/// </summary>
		public static List<T> GetInputNodes<T>(this Node current, string port) where T : class
		{
			List<T> castedNodes = new List<T>();

			IEnumerable<Node> inputNodes = current.GetInputPort(port).GetConnections().Select((c) => c.node);
			foreach (Node node in inputNodes)
			{
				if (node is T castedNode)
				{
					castedNodes.Add(castedNode);
				}
				else
				{
					Debug.LogError($"Connected node could not be cast to target type. Typeof: {node.GetType()}", current);
				}
			}

			return castedNodes;
		}

		/// <summary>
		/// Gets the node connected to the specified input port.
		/// </summary>
		public static T GetInputNode<T>(this Node current, string port) where T : class
		{
			NodePort nodePort = current.GetInputPort(port);
			if (nodePort == null || nodePort.Connection == null)
			{
				return null;
			}

			Node input = nodePort.Connection.node;
			if (input == null)
			{
				return null;
			}
			else if (input is T castedNode)
			{
				return castedNode;
			}
			else
			{
				Debug.LogWarning($"Connected node could not be cast to target type. Typeof: {input.GetType()}", current);
				return null;
			}
		}

		/// <summary>
		/// Gets all of the nodes connected to the specified output port.
		/// </summary>
		public static List<T> GetOutputNodes<T>(this Node current, string port) where T : class
		{
			List<T> castedNodes = new List<T>();

			IEnumerable<Node> outputNodes = current.GetOutputPort(port).GetConnections().Select((c) => c.node);
			foreach (Node node in outputNodes)
			{
				if (node is T castedNode)
				{
					castedNodes.Add(castedNode);
				}
				else
				{
					Debug.LogError($"Connected node could not be cast to target type. Typeof: {node.GetType()}", current);
				}
			}

			return castedNodes;
		}

		/// <summary>
		/// Gets the node connected to the specified output port.
		/// </summary>
		public static T GetOutputNode<T>(this Node current, string port) where T : class
		{
			NodePort nodePort = current.GetOutputPort(port);
			if (nodePort == null || nodePort.Connection == null)
			{
				return null;
			}

			Node output = nodePort.Connection.node;
			if (output == null)
			{
				return null;
			}
			else if (output is T castedNode)
			{
				return castedNode;
			}
			else
			{
				Debug.LogError($"Connected node could not be cast to target type. Typeof: {output.GetType()}", current);
				return null;
			}
		}

		/// <summary>
		/// Gets all of the nodes connected to any of the node's output ports.
		/// </summary>
		public static List<Node> GetAllOutputNodes(this Node current)
		{
			return current.Outputs.SelectMany((o) => o.GetConnections().Select((c) => c.node)).ToList();
		}
	}
}
