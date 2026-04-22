using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// GraphView that displays and edits a <see cref="GraphAsset"/>. Handles node creation,
	/// deletion, connection creation/deletion, and node movement, all with undo support.
	/// </summary>
	public class StateMachineGraphView : GraphView
	{
		private const float FIELDS_VISIBLE_SCALE = 0.5f;

		private GraphAsset graphAsset;
		private readonly Dictionary<string, StateMachineNodeView> nodeViews = new Dictionary<string, StateMachineNodeView>();
		private readonly Dictionary<string, Group> groupViews = new Dictionary<string, Group>();


		public StateMachineGraphView()
		{
			SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
			this.AddManipulator(new ContentDragger());
			this.AddManipulator(new SelectionDragger());
			this.AddManipulator(new RectangleSelector());

			var grid = new GridBackground();
			Insert(0, grid);
			grid.StretchToParentSize();

			graphViewChanged += OnGraphViewChanged;
			viewTransformChanged += OnZoomChanged;
		}

		private void OnZoomChanged(GraphView view)
		{
			bool visible = viewTransform.scale.x >= FIELDS_VISIBLE_SCALE;
			foreach (StateMachineNodeView nodeView in nodeViews.Values)
			{
				nodeView.SetFieldsVisible(visible);
			}
		}

		/// <summary>Loads a <see cref="GraphAsset"/> into the view, replacing any existing content.</summary>
		public void Load(GraphAsset asset)
		{
			graphAsset = asset;
			nodeViews.Clear();
			groupViews.Clear();
			graphViewChanged -= OnGraphViewChanged;
			DeleteElements(graphElements.ToList());
			graphViewChanged += OnGraphViewChanged;

			foreach (GraphNodeBase node in asset.Nodes)
			{
				if (node == null)
				{
					Debug.LogWarning($"[StateMachineGraphView] Null node in {asset.name} — script reference may be missing. Skipping.");
					continue;
				}

				if (string.IsNullOrEmpty(node.Guid))
				{
					Debug.LogWarning($"[StateMachineGraphView] Node '{node.name}' in {asset.name} has no GUID — run Tools/StateMachine/Repair Missing Node GUIDs. Skipping.");
					continue;
				}

				StateMachineNodeView view = CreateNodeView(node);
				nodeViews[node.Guid] = view;
			}

			foreach (GraphEdge edge in asset.Edges)
			{
				CreateEdgeView(edge);
			}

			foreach (GraphGroup data in asset.Groups)
			{
				Group g = CreateGroupView(data);
				foreach (string nodeGuid in data.NodeGuids)
				{
					if (nodeViews.TryGetValue(nodeGuid, out StateMachineNodeView nv))
					{
						g.AddElement(nv);
					}
				}
			}

			// Defer FrameAll until after the layout pass resolves node sizes.
			schedule.Execute(_ => FrameAll());
		}

		private StateMachineNodeView CreateNodeView(GraphNodeBase node)
		{
			StateMachineNodeView view = new StateMachineNodeView(node);
			AddElement(view);
			return view;
		}

		private Group CreateGroupView(GraphGroup data)
		{
			Group g = new Group { title = data.Title };
			g.SetPosition(data.Rect);
			AddElement(g);
			groupViews[data.Guid] = g;
			return g;
		}

		private GraphGroup FindGroupData(Group g)
		{
			foreach (GraphGroup data in graphAsset.Groups)
			{
				if (groupViews.TryGetValue(data.Guid, out Group view) && view == g)
				{
					return data;
				}
			}
			return null;
		}

		private void SyncGroupToData(Group g, GraphGroup data)
		{
			data.Rect = g.GetPosition();
			data.SetNodeGuids(g.containedElements
				.OfType<StateMachineNodeView>()
				.Select(v => v.Node.Guid));
			EditorUtility.SetDirty(graphAsset);
		}

		private void CreateEdgeView(GraphEdge edge)
		{
			if (!nodeViews.TryGetValue(edge.OutputNodeGuid, out StateMachineNodeView outputNodeView) ||
				!nodeViews.TryGetValue(edge.InputNodeGuid, out StateMachineNodeView inputNodeView))
			{
				Debug.LogWarning($"Could not find node views for edge {edge.OutputPortName} -> {edge.InputPortName}");
				return;
			}

			Port outputPort = outputNodeView.GetPort(edge.OutputPortName, Direction.Output);
			Port inputPort = inputNodeView.GetPort(edge.InputPortName, Direction.Input);

			if (outputPort == null || inputPort == null)
			{
				Debug.LogWarning($"Could not find ports for edge {edge.OutputPortName} -> {edge.InputPortName}");
				return;
			}

			Edge edgeView = outputPort.ConnectTo(inputPort);
			AddElement(edgeView);
		}

		private GraphViewChange OnGraphViewChanged(GraphViewChange change)
		{
			if (graphAsset == null)
			{
				return change;
			}

			if (change.edgesToCreate != null)
			{
				foreach (Edge edge in change.edgesToCreate)
				{
					StateMachineNodeView outputView = edge.output.node as StateMachineNodeView;
					StateMachineNodeView inputView = edge.input.node as StateMachineNodeView;

					if (outputView == null || inputView == null)
					{
						continue;
					}

					Undo.RecordObject(graphAsset, "Add Connection");
					GraphEdge graphEdge = new GraphEdge(
						outputView.Node.Guid, edge.output.portName,
						inputView.Node.Guid, edge.input.portName);
					graphAsset.AddEdge(graphEdge);
				}
			}

			if (change.elementsToRemove != null)
			{
				foreach (GraphElement elem in change.elementsToRemove)
				{
					if (elem is Edge edgeView)
					{
						StateMachineNodeView outputView = edgeView.output?.node as StateMachineNodeView;
						StateMachineNodeView inputView = edgeView.input?.node as StateMachineNodeView;

						if (outputView == null || inputView == null)
						{
							continue;
						}

						Undo.RecordObject(graphAsset, "Remove Connection");
						GraphEdge toRemove = graphAsset.Edges.FirstOrDefault(e =>
							e.OutputNodeGuid == outputView.Node.Guid &&
							e.OutputPortName == edgeView.output.portName &&
							e.InputNodeGuid == inputView.Node.Guid &&
							e.InputPortName == edgeView.input.portName);

						if (toRemove != null)
						{
							graphAsset.RemoveEdge(toRemove);
						}
					}
					else if (elem is StateMachineNodeView nodeView)
					{
						Undo.RecordObject(graphAsset, "Remove Node");
						if (!string.IsNullOrEmpty(nodeView.Node?.Guid))
							nodeViews.Remove(nodeView.Node.Guid);
						graphAsset.RemoveNode(nodeView.Node);
					}
					else if (elem is Group groupView)
					{
						GraphGroup data = FindGroupData(groupView);
						if (data != null)
						{
							Undo.RecordObject(graphAsset, "Remove Group");
							graphAsset.RemoveGroup(data);
							groupViews.Remove(data.Guid);
						}
					}
				}
			}

			if (change.movedElements != null)
			{
				int undoGroup = Undo.GetCurrentGroup();
				Undo.SetCurrentGroupName("Move Node");

				foreach (GraphElement elem in change.movedElements)
				{
					if (elem is StateMachineNodeView nodeView)
					{
						Undo.RecordObject(nodeView.Node, "Move Node");
						nodeView.Node.Position = nodeView.GetPosition().position;
						EditorUtility.SetDirty(nodeView.Node);
					}
				}

				// Sync all group positions and membership after any move (groups are few, this is cheap).
				foreach (GraphGroup data in graphAsset.Groups)
				{
					if (groupViews.TryGetValue(data.Guid, out Group g))
					{
						SyncGroupToData(g, data);
					}
				}

				Undo.CollapseUndoOperations(undoGroup);
			}

			return change;
		}

		public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
		{
			return ports.ToList().Where(p =>
				p.node != startPort.node &&
				p.direction != startPort.direction &&
				p.portType == startPort.portType
			).ToList();
		}

		public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
		{
			base.BuildContextualMenu(evt);

			if (graphAsset == null)
			{
				return;
			}

			Vector2 mousePos = evt.mousePosition;
			Vector2 worldPos = contentViewContainer.WorldToLocal(mousePos);

			IEnumerable<Type> nodeTypes = TypeCache.GetTypesDerivedFrom<StateMachineNodeBase>()
				.Where(t => !t.IsAbstract)
				.OrderBy(t => t.Name);

			foreach (Type type in nodeTypes)
			{
				string ns = type.Namespace ?? "";
				string stripped = ns.StartsWith("SpaxUtils.") ? ns.Substring("SpaxUtils.".Length) : ns;
				string subPath = string.IsNullOrEmpty(stripped) ? "" : stripped.Replace(".", "/") + "/";
				string label = $"Add Node/{subPath}{type.Name}";

				evt.menu.AppendAction(label, _ =>
				{
					Undo.RecordObject(graphAsset, "Add Node");
					GraphNodeBase node = graphAsset.AddNode(type, worldPos);
					StateMachineNodeView view = CreateNodeView(node);
					nodeViews[node.Guid] = view;
				});
			}

			evt.menu.AppendSeparator();
			evt.menu.AppendAction("Create Group", _ =>
			{
				List<string> selectedNodeGuids = selection
					.OfType<StateMachineNodeView>()
					.Select(v => v.Node.Guid)
					.ToList();

				int undoGroup = Undo.GetCurrentGroup();
				Undo.SetCurrentGroupName("Create Group");

				Undo.RecordObject(graphAsset, "Create Group");
				GraphGroup data = graphAsset.AddGroup("New Group", new Rect(worldPos, new Vector2(200, 150)));
				Group g = CreateGroupView(data);

				foreach (string guid in selectedNodeGuids)
				{
					if (nodeViews.TryGetValue(guid, out StateMachineNodeView nv))
					{
						g.AddElement(nv);
					}
				}

				SyncGroupToData(g, data);
				Undo.CollapseUndoOperations(undoGroup);
			});
		}
	}
}
