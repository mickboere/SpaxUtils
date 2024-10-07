using System.Collections.Generic;
using UnityEngine;
using XNode;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// <see cref="NodeGraph"/> implementation for state machine assets.
	/// </summary>
	[CreateAssetMenu(fileName = "StateMachineGraph", menuName = "StateMachine/StateMachineGraph")]
	public class StateMachineGraph : NodeGraph, IBindingKeyProvider
	{
		public object BindingKey => GetInstanceID();

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

		public bool ContainsNodeOfType<T>()
		{
			foreach (Node node in nodes)
			{
				if (node is T)
				{
					return true;
				}
			}
			return false;
		}
	}
}
