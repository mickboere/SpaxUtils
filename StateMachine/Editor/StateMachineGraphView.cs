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
		private NodeCreationSearchWindow searchWindow;


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

			searchWindow = ScriptableObject.CreateInstance<NodeCreationSearchWindow>();
			nodeCreationRequest = OnNodeCreationRequest;

			serializeGraphElements = OnSerializeElements;
			canPasteSerializedData = OnCanPaste;
			unserializeAndPaste = OnPaste;
		}

		private void OnNodeCreationRequest(NodeCreationContext context)
		{
			if (graphAsset == null)
			{
				return;
			}

			// Convert to graph content space while in the correct GUI context (the graph editor window).
			// GUIUtility.ScreenToGUIPoint converts OS screen coords to this window's GUI/panel world space.
			// ChangeCoordinatesTo then converts from panel world space to content container local space,
			// which accounts for pan and zoom. Both WorldToLocal and GetRootVisualElement were removed in Unity 6.
			Vector2 guiPos = GUIUtility.ScreenToGUIPoint(context.screenMousePosition);
			Vector2 contentPos = panel.visualTree.ChangeCoordinatesTo(contentViewContainer, guiPos);

			searchWindow.Initialize(this, graphAsset, context.target as Port, contentPos);
			SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), searchWindow);
		}

		internal StateMachineNodeView CreateNodeViewAndRegister(GraphNodeBase node)
		{
			StateMachineNodeView view = CreateNodeView(node);
			nodeViews[node.Guid] = view;
			return view;
		}

		internal Port FindFirstCompatiblePort(Port sourcePort, StateMachineNodeView targetView)
		{
			if (sourcePort.capacity == Port.Capacity.Single && sourcePort.connected)
			{
				return null;
			}

			Direction requiredDirection = sourcePort.direction == Direction.Output
				? Direction.Input
				: Direction.Output;

			return ports.ToList().FirstOrDefault(p =>
				p.node == targetView &&
				p.direction == requiredDirection &&
				p.portType == sourcePort.portType &&
				!(p.capacity == Port.Capacity.Single && p.connected));
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

			FrameFromData(asset);
		}

		// Frames the view to fit all nodes using stored position data rather than resolved
		// UIElements layout, which is zero until the first paint and makes FrameAll unreliable.
		private void FrameFromData(GraphAsset asset)
		{
			List<GraphNodeBase> nodes = asset.Nodes.Where(n => n != null).ToList();
			if (nodes.Count == 0)
			{
				return;
			}

			const float padding = 80f;
			const float estimatedNodeSize = 200f;

			float minX = float.MaxValue, minY = float.MaxValue;
			float maxX = float.MinValue, maxY = float.MinValue;

			foreach (GraphNodeBase node in nodes)
			{
				minX = Mathf.Min(minX, node.Position.x);
				minY = Mathf.Min(minY, node.Position.y);
				maxX = Mathf.Max(maxX, node.Position.x + estimatedNodeSize);
				maxY = Mathf.Max(maxY, node.Position.y + estimatedNodeSize);
			}

			Rect contentBounds = Rect.MinMaxRect(minX - padding, minY - padding, maxX + padding, maxY + padding);

			// Use the graphView's layout if available, otherwise fall back to the panel root
			// (the full editor window content area) which is sized before child layout runs.
			float viewW = layout.width > 0f ? layout.width : panel?.visualTree.layout.width ?? 0f;
			float viewH = layout.height > 0f ? layout.height : panel?.visualTree.layout.height ?? 0f;

			if (viewW <= 0f || viewH <= 0f)
			{
				// Panel not ready either — defer once and retry.
				schedule.Execute(_ => FrameFromData(asset));
				return;
			}

			float zoom = Mathf.Clamp(
				Mathf.Min(viewW / contentBounds.width, viewH / contentBounds.height),
				ContentZoomer.DefaultMinScale,
				ContentZoomer.DefaultMaxScale);

			float panX = viewW * 0.5f - contentBounds.center.x * zoom;
			float panY = viewH * 0.5f - contentBounds.center.y * zoom;

			UpdateViewTransform(new Vector3(panX, panY, 0f), new Vector3(zoom, zoom, 1f));
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

		private string OnSerializeElements(IEnumerable<GraphElement> elements)
		{
			var data = new CopyPasteData();
			List<StateMachineNodeView> views = elements.OfType<StateMachineNodeView>().ToList();
			var guids = new HashSet<string>(views.Select(v => v.Node.Guid));

			foreach (StateMachineNodeView view in views)
			{
				data.nodes.Add(new SerializedNodeData
				{
					typeName = view.Node.GetType().AssemblyQualifiedName,
					originalGuid = view.Node.Guid,
					position = view.Node.Position,
					nodeJson = EditorJsonUtility.ToJson(view.Node)
				});
			}

			foreach (GraphEdge edge in graphAsset.Edges)
			{
				if (guids.Contains(edge.OutputNodeGuid) && guids.Contains(edge.InputNodeGuid))
				{
					data.edges.Add(new SerializedEdgeData
					{
						outputGuid = edge.OutputNodeGuid,
						outputPort = edge.OutputPortName,
						inputGuid = edge.InputNodeGuid,
						inputPort = edge.InputPortName
					});
				}
			}

			return JsonUtility.ToJson(data);
		}

		private bool OnCanPaste(string data)
		{
			if (string.IsNullOrEmpty(data))
			{
				return false;
			}

			try
			{
				CopyPasteData parsed = JsonUtility.FromJson<CopyPasteData>(data);
				return parsed?.nodes?.Count > 0;
			}
			catch
			{
				return false;
			}
		}

		private void OnPaste(string operationName, string data)
		{
			CopyPasteData copyData = JsonUtility.FromJson<CopyPasteData>(data);
			if (copyData?.nodes == null || copyData.nodes.Count == 0)
			{
				return;
			}

			const float pasteOffset = 40f;
			var guidMap = new Dictionary<string, string>();
			var newViews = new List<StateMachineNodeView>();

			int undoGroup = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Paste Nodes");
			Undo.RecordObject(graphAsset, "Paste Nodes");

			foreach (SerializedNodeData nodeData in copyData.nodes)
			{
				Type type = Type.GetType(nodeData.typeName);
				if (type == null)
				{
					continue;
				}

				Vector2 newPos = nodeData.position + new Vector2(pasteOffset, pasteOffset);
				GraphNodeBase newNode = graphAsset.AddNode(type, newPos);
				CopyNodeFields(nodeData.nodeJson, newNode);
				newNode.Position = newPos;

				guidMap[nodeData.originalGuid] = newNode.Guid;
				newViews.Add(CreateNodeViewAndRegister(newNode));
			}

			foreach (SerializedEdgeData edgeData in copyData.edges)
			{
				if (!guidMap.TryGetValue(edgeData.outputGuid, out string outGuid)) continue;
				if (!guidMap.TryGetValue(edgeData.inputGuid, out string inGuid)) continue;
				if (!nodeViews.TryGetValue(outGuid, out StateMachineNodeView outView)) continue;
				if (!nodeViews.TryGetValue(inGuid, out StateMachineNodeView inView)) continue;

				Port outPort = outView.GetPort(edgeData.outputPort, Direction.Output);
				Port inPort = inView.GetPort(edgeData.inputPort, Direction.Input);
				if (outPort == null || inPort == null) continue;

				Undo.RecordObject(graphAsset, "Paste Connection");
				graphAsset.AddEdge(new GraphEdge(outGuid, edgeData.outputPort, inGuid, edgeData.inputPort));

				Edge edgeView = outPort.ConnectTo(inPort);
				AddElement(edgeView);
			}

			Undo.CollapseUndoOperations(undoGroup);

			ClearSelection();
			foreach (StateMachineNodeView view in newViews)
			{
				AddToSelection(view);
			}
		}

		// Copies serialized data fields from sourceJson into dest, preserving dest's guid,
		// position, and graph reference. Uses a temp object as deserialization intermediary.
		private static void CopyNodeFields(string sourceJson, GraphNodeBase dest)
		{
			GraphNodeBase temp = (GraphNodeBase)ScriptableObject.CreateInstance(dest.GetType());
			EditorJsonUtility.FromJsonOverwrite(sourceJson, temp);

			SerializedObject srcSO = new SerializedObject(temp);
			SerializedObject dstSO = new SerializedObject(dest);

			SerializedProperty it = srcSO.GetIterator();
			if (it.NextVisible(true))
			{
				do
				{
					if (it.name != "m_Script" && it.name != "guid" && it.name != "position" && it.name != "graph")
					{
						dstSO.CopyFromSerializedProperty(it);
					}
				}
				while (it.NextVisible(false));
			}

			dstSO.ApplyModifiedProperties();
			UnityEngine.Object.DestroyImmediate(temp);
		}

		[Serializable]
		private class CopyPasteData
		{
			public List<SerializedNodeData> nodes = new List<SerializedNodeData>();
			public List<SerializedEdgeData> edges = new List<SerializedEdgeData>();
		}

		[Serializable]
		private class SerializedNodeData
		{
			public string typeName;
			public string originalGuid;
			public Vector2 position;
			public string nodeJson;
		}

		[Serializable]
		private class SerializedEdgeData
		{
			public string outputGuid;
			public string outputPort;
			public string inputGuid;
			public string inputPort;
		}
	}
}
