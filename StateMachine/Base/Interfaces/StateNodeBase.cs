using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Base State Node class, defining an enterable state within the flow.
	/// </summary>
	[NodeWidth(200)]
	public abstract class StateNodeBase : StateMachineNodeBase, IState
	{
		public virtual string ID => id;
		public bool Active { get; private set; }

		public IState Parent => _parent ?? GetInputNode<IState>(nameof(inConnection));
		private IState _parent;

		public IState DefaultChild => string.IsNullOrEmpty(_defaultChild) ? null : Children[_defaultChild];
		protected virtual string _defaultChild { get; private set; }

		public IReadOnlyDictionary<string, IState> Children => _children ?? GetChildren().ToDictionary((s) => s.ID);
		private Dictionary<string, IState> _children;

		public IReadOnlyCollection<IStateComponent> Components => _components ?? GetComponents();
		private IReadOnlyCollection<IStateComponent> _components;

		[SerializeField, HideInInspector] private string id;
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] private Connections.State inConnection;
		[SerializeField, Output(backingValue = ShowBackingValue.Never, typeConstraint = TypeConstraint.Inherited)] private Connections.StateComponent components;

		protected override void Init()
		{
			EnsureUniqueId();
			base.Init();
		}

		public void InjectDependencies(IDependencyManager dependencyManager)
		{
			_parent = GetInputNode<IState>(nameof(inConnection));
			_children = GetChildren().ToDictionary((s) => s.ID);
			_components = GetComponents().ToList();

			foreach (IStateComponent component in _components)
			{
				dependencyManager.Inject(component);
			}
		}

		#region Callbacks

		/// <inheritdoc/>
		public override void OnEnteringState()
		{
			foreach (IStateComponent component in _components)
			{
				component.OnEnteringState();
			}
		}

		/// <inheritdoc/>
		public override void WhileEnteringState(ITransition transition)
		{
			foreach (IStateComponent component in _components)
			{
				component.WhileEnteringState(transition);
			}
		}

		/// <inheritdoc/>
		public override void OnStateEntered()
		{
			Active = true;
			foreach (IStateComponent component in _components)
			{
				component.OnStateEntered();
			}
		}

		/// <inheritdoc/>
		public override void OnExitingState()
		{
			foreach (IStateComponent component in _components)
			{
				component.OnExitingState();
			}
		}

		/// <inheritdoc/>
		public override void WhileExitingState(ITransition transition)
		{
			foreach (IStateComponent component in _components)
			{
				component.WhileExitingState(transition);
			}
		}

		/// <inheritdoc/>
		public override void OnStateExit()
		{
			Active = false;
			foreach (IStateComponent component in _components)
			{
				component.OnStateExit();
			}
		}

		#endregion

		public void SetParent(IState parent)
		{
			SpaxDebug.Error("Nodes are not dynamic.", "Could not set parent.");
		}

		public void SetDefaultChild(string id)
		{
			_defaultChild = id;
		}

		public void AddChild(IState child)
		{
			SpaxDebug.Error("Nodes are not dynamic.", "Could not set add child.");
		}

		public void RemoveChild(string id)
		{
			SpaxDebug.Error("Nodes are not dynamic.", "Could not remove child.");
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
					if (graph.nodes.Any((node) => node is IState state && state != this && state.ID == id))
					{
						id = Guid.NewGuid().ToString();
					}
				}
			}
		}

		public IReadOnlyCollection<IState> GetChildren()
		{
			return XNodeExtensions.GetAllOutputNodes(this).Where((c) => c is IState).Cast<IState>().ToHashSet();
		}
	}
}
