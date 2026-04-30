using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Edge connector listener that fires <see cref="GraphView.nodeCreationRequest"/> when a port
	/// drag is released over empty space, enabling the add-node search menu.
	/// OnDrop replicates the default GraphView behaviour so normal edge connections still work.
	/// </summary>
	internal class StateMachineEdgeConnectorListener : IEdgeConnectorListener
	{
		private readonly GraphViewChange graphViewChange;
		private readonly List<Edge> edgesToCreate;
		private readonly List<GraphElement> edgesToDelete;

		internal StateMachineEdgeConnectorListener()
		{
			edgesToCreate = new List<Edge>();
			edgesToDelete = new List<GraphElement>();
			graphViewChange = new GraphViewChange { edgesToCreate = edgesToCreate };
		}

		public void OnDropOutsidePort(Edge edge, Vector2 position)
		{
			// 'position' is in the GraphView's local coordinate space (no pan/zoom applied).
			Port startPort = edge.output ?? edge.input;
			StateMachineGraphView graphView = startPort?.GetFirstAncestorOfType<StateMachineGraphView>();
			if (graphView?.nodeCreationRequest == null)
			{
				return;
			}

			// Convert graphView-local → panel world space → OS screen space for SearchWindow.Open.
			Vector2 panelPos = graphView.ChangeCoordinatesTo(graphView.panel.visualTree, position);
			Vector2 screenPos = GUIUtility.GUIToScreenPoint(panelPos);

			graphView.nodeCreationRequest(new NodeCreationContext
			{
				screenMousePosition = screenPos,
				target = startPort,
				index = 0
			});
		}

		public void OnDrop(GraphView graphView, Edge edge)
		{
			edgesToCreate.Clear();
			edgesToCreate.Add(edge);
			edgesToDelete.Clear();

			if (edge.input.capacity == Port.Capacity.Single)
			{
				foreach (Edge existing in edge.input.connections)
				{
					if (existing != edge)
					{
						edgesToDelete.Add(existing);
					}
				}
			}

			if (edge.output.capacity == Port.Capacity.Single)
			{
				foreach (Edge existing in edge.output.connections)
				{
					if (existing != edge)
					{
						edgesToDelete.Add(existing);
					}
				}
			}

			if (edgesToDelete.Count > 0)
			{
				graphView.DeleteElements(edgesToDelete);
			}

			List<Edge> toCreate = edgesToCreate;
			if (graphView.graphViewChanged != null)
			{
				toCreate = graphView.graphViewChanged(graphViewChange).edgesToCreate;
			}

			foreach (Edge e in toCreate)
			{
				graphView.AddElement(e);
				e.input.Connect(e);
				e.output.Connect(e);
			}
		}
	}
}
