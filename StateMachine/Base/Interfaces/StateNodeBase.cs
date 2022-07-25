using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils.StateMachine
{
	/// <summary>
	/// Base State Node class, defining an enterable state within the flow.
	/// </summary>
	[NodeWidth(200)]
	public abstract class StateNodeBase : StateMachineNodeBase, IState
	{
		/// <inheritdoc/>
		public virtual string Name => UID;

		/// <inheritdoc/>
		public string UID => id;

		[SerializeField, HideInInspector] private string id;
		[SerializeField, Output(backingValue = ShowBackingValue.Never, typeConstraint = TypeConstraint.Inherited)] private Connections.StateComponent components;

		protected override void Init()
		{
			EnsureUniqueId();
			base.Init();
		}

		/// <inheritdoc/>
		public virtual List<IStateComponent> GetAllComponents()
		{
			return this.GetAllChildComponents();
		}

		private void EnsureUniqueId()
		{
			if (!Application.isPlaying)
			{
				if (id == null)
				{
					id = Guid.NewGuid().ToString();
				}
				else
				{
					if (graph.nodes.Any((node) => node is IState state && state != this && state.UID == id))
					{
						id = Guid.NewGuid().ToString();
					}
				}
			}
		}
	}
}
