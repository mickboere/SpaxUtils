using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Base ScriptableObject for all graph assets. Replaces the XNode.NodeGraph dependency.
	/// Owns a list of <see cref="GraphNodeBase"/> sub-assets and a list of <see cref="GraphEdge"/>
	/// records that describe the directed connections between them.
	/// </summary>
	public abstract class GraphAsset : ScriptableObject
	{
		[SerializeField] private List<GraphNodeBase> nodes = new List<GraphNodeBase>();
		[SerializeField] private List<GraphEdge> edges = new List<GraphEdge>();
		[SerializeField] private List<GraphGroup> groups = new List<GraphGroup>();

		public IReadOnlyList<GraphNodeBase> Nodes => nodes;
		public IReadOnlyList<GraphEdge> Edges => edges;
		public IReadOnlyList<GraphGroup> Groups => groups;

		/// <summary>
		/// Creates a node of the given <paramref name="type"/>, registers it as a sub-asset of
		/// this graph, and returns the new instance. Editor-only.
		/// </summary>
		public GraphNodeBase AddNode(System.Type type, Vector2 position)
		{
			GraphNodeBase node = (GraphNodeBase)CreateInstance(type);
			node.Graph = this;
			node.Position = position;
			node.OnCreated();
			nodes.Add(node);
#if UNITY_EDITOR
			AssetDatabase.AddObjectToAsset(node, this);
			EditorUtility.SetDirty(this);
#endif
			return node;
		}

		/// <summary>
		/// Creates a node of type <typeparamref name="T"/>, registers it as a sub-asset of this
		/// graph, and returns the new instance.
		/// </summary>
		public T AddNode<T>(Vector2 position) where T : GraphNodeBase
		{
			T node = CreateInstance<T>();
			node.Graph = this;
			node.Position = position;
			node.OnCreated();
			nodes.Add(node);
#if UNITY_EDITOR
			AssetDatabase.AddObjectToAsset(node, this);
			EditorUtility.SetDirty(this);
#endif
			return node;
		}

		/// <summary>
		/// Removes <paramref name="node"/> from the graph, cleans up all connected edges,
		/// and destroys the sub-asset.
		/// </summary>
		public void RemoveNode(GraphNodeBase node)
		{
			if (!nodes.Contains(node))
			{
				return;
			}

			edges.RemoveAll(e => e.OutputNodeGuid == node.Guid || e.InputNodeGuid == node.Guid);
			nodes.Remove(node);

#if UNITY_EDITOR
			AssetDatabase.RemoveObjectFromAsset(node);
			DestroyImmediate(node, true);
			EditorUtility.SetDirty(this);
#endif
		}

		/// <summary>Adds a new visual group and returns it.</summary>
		public GraphGroup AddGroup(string title, Rect rect)
		{
			GraphGroup group = new GraphGroup(title, rect);
			groups.Add(group);
#if UNITY_EDITOR
			EditorUtility.SetDirty(this);
#endif
			return group;
		}

		/// <summary>Removes a group (nodes are unaffected).</summary>
		public void RemoveGroup(GraphGroup group)
		{
			groups.Remove(group);
#if UNITY_EDITOR
			EditorUtility.SetDirty(this);
#endif
		}

		/// <summary>Adds a directed edge between two ports.</summary>
		public void AddEdge(GraphEdge edge)
		{
			edges.Add(edge);
#if UNITY_EDITOR
			EditorUtility.SetDirty(this);
#endif
		}

		/// <summary>Removes a specific edge by reference.</summary>
		public void RemoveEdge(GraphEdge edge)
		{
			edges.Remove(edge);
#if UNITY_EDITOR
			EditorUtility.SetDirty(this);
#endif
		}

		/// <summary>Returns all nodes in this graph that are assignable to <typeparamref name="T"/>.</summary>
		public List<T> GetNodesOfType<T>()
		{
			List<T> result = new List<T>();
			foreach (GraphNodeBase node in nodes)
			{
				if (node is T cast)
				{
					result.Add(cast);
				}
			}
			return result;
		}

		/// <summary>Returns true if this graph contains at least one node assignable to <typeparamref name="T"/>.</summary>
		public bool ContainsNodeOfType<T>()
		{
			foreach (GraphNodeBase node in nodes)
			{
				if (node is T)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Creates a deep runtime copy of this graph. Each node is instantiated separately so the
		/// copy is fully independent. Edge data is copied by value and remains valid because node
		/// GUIDs are preserved across instantiation.
		/// </summary>
		public virtual GraphAsset Copy()
		{
			GraphAsset copy = Instantiate(this);

			List<GraphNodeBase> originals = new List<GraphNodeBase>(nodes);
			copy.nodes.Clear();

			foreach (GraphNodeBase original in originals)
			{
				if (original == null)
				{
					continue;
				}

				GraphNodeBase nodeCopy = Instantiate(original);
				nodeCopy.Graph = copy;
				copy.nodes.Add(nodeCopy);
			}

			return copy;
		}

		protected virtual void OnDestroy()
		{
			foreach (GraphNodeBase node in nodes)
			{
				if (node != null)
				{
					Destroy(node);
				}
			}
		}
	}
}
