using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Search window shown when dragging a port edge into empty graph space.
	/// Filters node types to those with a compatible port and auto-connects on selection.
	/// </summary>
	public class NodeCreationSearchWindow : ScriptableObject, ISearchWindowProvider
	{
		private const int FLAT_LIST_THRESHOLD = 6;

		private StateMachineGraphView graphView;
		private GraphAsset graphAsset;
		private Port sourcePort;
		private Vector2 nodePosition; // pre-converted graph content space position

		public void Initialize(StateMachineGraphView graphView, GraphAsset graphAsset,
			Port sourcePort, Vector2 nodePosition)
		{
			this.graphView = graphView;
			this.graphAsset = graphAsset;
			this.sourcePort = sourcePort;
			this.nodePosition = nodePosition;
		}

		public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
		{
			var tree = new List<SearchTreeEntry>
			{
				new SearchTreeGroupEntry(new GUIContent("Add Node"), 0)
			};

			List<Type> types = GetFilteredNodeTypes().ToList();

			if (types.Count == 0)
			{
				tree.Add(new SearchTreeEntry(new GUIContent("No compatible nodes")) { level = 1 });
				return tree;
			}

			// Small result sets are shown flat — no need to navigate into namespace folders.
			if (types.Count <= FLAT_LIST_THRESHOLD)
			{
				foreach (Type type in types.OrderBy(t => t.Name))
				{
					tree.Add(new SearchTreeEntry(new GUIContent(type.Name)) { level = 1, userData = type });
				}
				return tree;
			}

			// Group by stripped namespace, matching BuildContextualMenu's subPath logic.
			var byGroup = new Dictionary<string, List<Type>>();
			foreach (Type type in types)
			{
				string ns = type.Namespace ?? "";
				string stripped = ns.StartsWith("SpaxUtils.") ? ns.Substring("SpaxUtils.".Length) : ns;
				string groupKey = stripped.Replace(".", "/");
				if (!byGroup.TryGetValue(groupKey, out List<Type> bucket))
				{
					bucket = new List<Type>();
					byGroup[groupKey] = bucket;
				}
				bucket.Add(type);
			}

			foreach (KeyValuePair<string, List<Type>> group in byGroup.OrderBy(g => g.Key))
			{
				if (!string.IsNullOrEmpty(group.Key))
				{
					tree.Add(new SearchTreeGroupEntry(new GUIContent(group.Key), 1));
					foreach (Type type in group.Value.OrderBy(t => t.Name))
					{
						tree.Add(new SearchTreeEntry(new GUIContent(type.Name)) { level = 2, userData = type });
					}
				}
				else
				{
					foreach (Type type in group.Value.OrderBy(t => t.Name))
					{
						tree.Add(new SearchTreeEntry(new GUIContent(type.Name)) { level = 1, userData = type });
					}
				}
			}

			return tree;
		}

		public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
		{
			if (entry.userData is not Type type)
			{
				return false;
			}

			int undoGroup = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Add Node and Connect");

			Undo.RecordObject(graphAsset, "Add Node");
			GraphNodeBase newNode = graphAsset.AddNode(type, nodePosition);
			StateMachineNodeView newView = graphView.CreateNodeViewAndRegister(newNode);

			if (sourcePort != null)
			{
				Port targetPort = graphView.FindFirstCompatiblePort(sourcePort, newView);
				if (targetPort != null)
				{
					Port outputPort = sourcePort.direction == Direction.Output ? sourcePort : targetPort;
					Port inputPort = sourcePort.direction == Direction.Input ? sourcePort : targetPort;

					Edge edgeView = outputPort.ConnectTo(inputPort);
					graphView.AddElement(edgeView);

					StateMachineNodeView outputView = outputPort.node as StateMachineNodeView;
					StateMachineNodeView inputView = inputPort.node as StateMachineNodeView;

					if (outputView != null && inputView != null)
					{
						Undo.RecordObject(graphAsset, "Add Connection");
						graphAsset.AddEdge(new GraphEdge(
							outputView.Node.Guid, outputPort.portName,
							inputView.Node.Guid, inputPort.portName));
					}
				}
			}

			Undo.CollapseUndoOperations(undoGroup);
			return true;
		}

		private IEnumerable<Type> GetFilteredNodeTypes()
		{
			IEnumerable<Type> all = TypeCache.GetTypesDerivedFrom<StateMachineNodeBase>()
				.Where(t => !t.IsAbstract);

			if (sourcePort == null)
			{
				return all;
			}

			bool needInput = sourcePort.direction == Direction.Output;
			return all.Where(t => HasCompatiblePort(t, sourcePort.portType, needInput));
		}

		private static bool HasCompatiblePort(Type nodeType, Type portType, bool needInput)
		{
			foreach (FieldInfo field in GetAllFields(nodeType))
			{
				if (field.FieldType != portType)
				{
					continue;
				}

				if (needInput && field.GetCustomAttribute<NodeInputAttribute>() != null)
				{
					return true;
				}

				if (!needInput && field.GetCustomAttribute<NodeOutputAttribute>() != null)
				{
					return true;
				}
			}
			return false;
		}

		private static IEnumerable<FieldInfo> GetAllFields(Type type)
		{
			if (type == null || type == typeof(ScriptableObject) || type == typeof(UnityEngine.Object))
			{
				return Enumerable.Empty<FieldInfo>();
			}

			IEnumerable<FieldInfo> own = type.GetFields(
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);

			return GetAllFields(type.BaseType).Concat(own);
		}
	}
}
