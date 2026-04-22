using System;
using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Serializable record of a single directed connection between two node ports.
	/// The connection runs from an output port to an input port.
	/// </summary>
	[Serializable]
	public class GraphEdge
	{
		[SerializeField] private string outputNodeGuid;
		[SerializeField] private string outputPortName;
		[SerializeField] private string inputNodeGuid;
		[SerializeField] private string inputPortName;

		public string OutputNodeGuid => outputNodeGuid;
		public string OutputPortName => outputPortName;
		public string InputNodeGuid => inputNodeGuid;
		public string InputPortName => inputPortName;

		public GraphEdge(string outputNodeGuid, string outputPortName, string inputNodeGuid, string inputPortName)
		{
			this.outputNodeGuid = outputNodeGuid;
			this.outputPortName = outputPortName;
			this.inputNodeGuid = inputNodeGuid;
			this.inputPortName = inputPortName;
		}
	}
}
