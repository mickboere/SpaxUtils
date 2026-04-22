using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Base class for all graph nodes. Replaces the XNode.Node dependency.
	/// Stores a GUID and position for editor use, and provides typed port traversal
	/// by querying the parent <see cref="GraphAsset"/>'s edge list.
	/// </summary>
	public abstract class GraphNodeBase : ScriptableObject
	{
		[SerializeField, HideInInspector] private string guid;
		[SerializeField, HideInInspector] private Vector2 position;
		[SerializeField, HideInInspector] private GraphAsset graph;

		public string Guid => guid;
		public Vector2 Position { get => position; set => position = value; }
		public GraphAsset Graph { get => graph; set => graph = value; }

		/// <summary>
		/// Called by <see cref="GraphAsset.AddNode{T}"/> immediately after creation.
		/// Override to set the node's display name or other creation-time defaults.
		/// </summary>
		public virtual void OnCreated()
		{
			guid = System.Guid.NewGuid().ToString();
		}

		/// <summary>
		/// Returns the single node connected to this node's input port named <paramref name="port"/>,
		/// or null if nothing is connected or the connected node is not of type <typeparamref name="T"/>.
		/// </summary>
		public T GetInputNode<T>(string port) where T : class
		{
			if (graph == null)
			{
				return null;
			}

			foreach (GraphEdge edge in graph.Edges.Where(e => e.InputNodeGuid == guid && e.InputPortName == port))
			{
				GraphNodeBase node = graph.Nodes.FirstOrDefault(n => n.Guid == edge.OutputNodeGuid);
				if (node is T cast)
				{
					return cast;
				}
			}

			return null;
		}

		/// <summary>
		/// Returns all nodes connected to this node's input port named <paramref name="port"/>
		/// that satisfy the optional <paramref name="eval"/> predicate.
		/// </summary>
		public List<T> GetInputNodes<T>(string port, Func<T, bool> eval = null) where T : class
		{
			if (graph == null)
			{
				return new List<T>();
			}

			if (eval == null)
			{
				eval = _ => true;
			}

			List<T> result = new List<T>();
			foreach (GraphEdge edge in graph.Edges.Where(e => e.InputNodeGuid == guid && e.InputPortName == port))
			{
				GraphNodeBase node = graph.Nodes.FirstOrDefault(n => n.Guid == edge.OutputNodeGuid);
				if (node is T cast && eval(cast))
				{
					result.Add(cast);
				}
			}

			return result;
		}

		/// <summary>
		/// Returns the single node connected to this node's output port named <paramref name="port"/>,
		/// or null if nothing is connected or the connected node is not of type <typeparamref name="T"/>.
		/// </summary>
		public T GetOutputNode<T>(string port) where T : class
		{
			if (graph == null)
			{
				return null;
			}

			foreach (GraphEdge edge in graph.Edges.Where(e => e.OutputNodeGuid == guid && e.OutputPortName == port))
			{
				GraphNodeBase node = graph.Nodes.FirstOrDefault(n => n.Guid == edge.InputNodeGuid);
				if (node is T cast)
				{
					return cast;
				}
			}

			return null;
		}

		/// <summary>
		/// Returns all nodes connected to this node's output port named <paramref name="port"/>
		/// that satisfy the optional <paramref name="eval"/> predicate.
		/// </summary>
		public List<T> GetOutputNodes<T>(string port, Func<T, bool> eval = null) where T : class
		{
			if (graph == null)
			{
				return new List<T>();
			}

			if (eval == null)
			{
				eval = _ => true;
			}

			List<T> result = new List<T>();
			foreach (GraphEdge edge in graph.Edges.Where(e => e.OutputNodeGuid == guid && e.OutputPortName == port))
			{
				GraphNodeBase node = graph.Nodes.FirstOrDefault(n => n.Guid == edge.InputNodeGuid);
				if (node is T cast && eval(cast))
				{
					result.Add(cast);
				}
			}

			return result;
		}

		/// <summary>
		/// Returns all nodes reachable via any of this node's output ports.
		/// </summary>
		public List<GraphNodeBase> GetAllOutputNodes()
		{
			if (graph == null)
			{
				return new List<GraphNodeBase>();
			}

			List<GraphNodeBase> result = new List<GraphNodeBase>();
			foreach (GraphEdge edge in graph.Edges.Where(e => e.OutputNodeGuid == guid))
			{
				GraphNodeBase node = graph.Nodes.FirstOrDefault(n => n.Guid == edge.InputNodeGuid);
				if (node != null)
				{
					result.Add(node);
				}
			}

			return result;
		}
	}
}
