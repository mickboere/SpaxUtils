using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// GraphView node visual for a <see cref="GraphNodeBase"/> instance.
	/// Discovers ports via <see cref="NodeInputAttribute"/>/<see cref="NodeOutputAttribute"/> reflection
	/// and displays all other serialized fields as an inline inspector.
	/// </summary>
	public class StateMachineNodeView : UnityEditor.Experimental.GraphView.Node
	{
		public GraphNodeBase Node { get; }

		private readonly Dictionary<string, Port> inputPortsByName = new Dictionary<string, Port>();
		private readonly Dictionary<string, Port> outputPortsByName = new Dictionary<string, Port>();

		private static readonly Color StatePortColor = new Color(0.29f, 0.56f, 0.85f);
		private static readonly Color ComponentPortColor = new Color(0.36f, 0.72f, 0.36f);
		private static readonly Color RulePortColor = new Color(0.94f, 0.68f, 0.31f);

		public StateMachineNodeView(GraphNodeBase node)
		{
			Node = node;
			title = node.name;

			ApplyStyling(node.GetType());
			SetPosition(new Rect(node.Position, Vector2.zero));
			CreatePorts(node);
			PopulateFields(node);

			RefreshExpandedState();
			RefreshPorts();
		}

		public void SetFieldsVisible(bool visible)
		{
			extensionContainer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}

		/// <summary>Returns the port on this node with the given field name and direction, or null.</summary>
		public Port GetPort(string fieldName, Direction direction)
		{
			if (direction == Direction.Input)
			{
				return inputPortsByName.TryGetValue(fieldName, out Port p) ? p : null;
			}

			return outputPortsByName.TryGetValue(fieldName, out Port p2) ? p2 : null;
		}

		private void ApplyStyling(Type type)
		{
			NodeTintAttribute tint = type.GetCustomAttribute<NodeTintAttribute>();
			if (tint != null)
			{
				titleContainer.style.backgroundColor = new StyleColor(tint.color);
			}

			style.minWidth = 150;
			NodeWidthAttribute width = type.GetCustomAttribute<NodeWidthAttribute>();
			if (width != null)
			{
				style.minWidth = width.width;
			}
		}

		private void CreatePorts(GraphNodeBase node)
		{
			HashSet<string> portFieldNames = new HashSet<string>();

			foreach (FieldInfo field in GetAllFields(node.GetType()))
			{
				NodeInputAttribute inputAttr = field.GetCustomAttribute<NodeInputAttribute>();
				NodeOutputAttribute outputAttr = field.GetCustomAttribute<NodeOutputAttribute>();

				if (inputAttr != null)
				{
					Port port = InstantiatePort(
						Orientation.Horizontal,
						Direction.Input,
						inputAttr.connectionType == ConnectionType.Override ? Port.Capacity.Single : Port.Capacity.Multi,
						field.FieldType);
					port.portName = field.Name;
					port.portColor = GetPortColor(field.FieldType);
					inputContainer.Add(port);
					inputPortsByName[field.Name] = port;
					portFieldNames.Add(field.Name);
				}
				else if (outputAttr != null)
				{
					Port port = InstantiatePort(
						Orientation.Horizontal,
						Direction.Output,
						outputAttr.connectionType == ConnectionType.Override ? Port.Capacity.Single : Port.Capacity.Multi,
						field.FieldType);
					port.portName = field.Name;
					port.portColor = GetPortColor(field.FieldType);
					outputContainer.Add(port);
					outputPortsByName[field.Name] = port;
					portFieldNames.Add(field.Name);
				}
			}
		}

		private void PopulateFields(GraphNodeBase node)
		{
			HashSet<string> hiddenFieldNames = CollectPortAndHiddenFieldNames(node.GetType());
			SerializedObject serializedNode = new SerializedObject(node);

			// Pre-collect top-level properties once so the IMGUI handler never allocates.
			List<SerializedProperty> props = new List<SerializedProperty>();
			SerializedProperty it = serializedNode.GetIterator();
			if (it.NextVisible(true))
			{
				do
				{
					if (it.name != "m_Script" && !hiddenFieldNames.Contains(it.name))
					{
						props.Add(it.Copy());
					}
				}
				while (it.NextVisible(false));
			}

			if (props.Count == 0)
			{
				return;
			}

			// Single IMGUIContainer: IMGUI manages all sub-rects (including list items),
			// so custom IMGUI drawers always receive a properly-sized position rect.
			IMGUIContainer container = new IMGUIContainer(() =>
			{
				if (serializedNode.targetObject == null)
				{
					return;
				}

				serializedNode.UpdateIfRequiredOrScript();

				foreach (SerializedProperty prop in props)
				{
					EditorGUILayout.PropertyField(prop, true);
				}

				serializedNode.ApplyModifiedProperties();
			});

			// Prevent mouse events from reaching SelectionDragger (list reorder, field clicks).
			container.RegisterCallback<MouseDownEvent>(evt => evt.StopImmediatePropagation());
			extensionContainer.Add(container);
		}

		private HashSet<string> CollectPortAndHiddenFieldNames(Type type)
		{
			HashSet<string> names = new HashSet<string>();

			foreach (FieldInfo field in GetAllFields(type))
			{
				if (field.GetCustomAttribute<NodeInputAttribute>() != null ||
					field.GetCustomAttribute<NodeOutputAttribute>() != null ||
					field.GetCustomAttribute<HideInInspector>() != null)
				{
					names.Add(field.Name);
				}
			}

			return names;
		}

		private static Color GetPortColor(Type portType)
		{
			if (portType == typeof(Connections.State))
			{
				return StatePortColor;
			}

			if (portType == typeof(Connections.StateComponent))
			{
				return ComponentPortColor;
			}

			if (portType == typeof(Connections.Rule))
			{
				return RulePortColor;
			}

			return Color.gray;
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
