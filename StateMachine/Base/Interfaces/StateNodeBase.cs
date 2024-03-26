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
		public IState Parent { get; protected set; }

		public IState DefaultChild => string.IsNullOrEmpty(_defaultChild) ? null : Children[_defaultChild];
		private string _defaultChild;

		public IReadOnlyDictionary<string, IState> Children
		{
			get
			{
				EnsureChildren();
				return _children;
			}
		}
		private Dictionary<string, IState> _children;

		public IReadOnlyCollection<IStateComponent> Components
		{
			get
			{
				if (_components == null)
				{
					_components = GetComponents();
				}
				return _components;
			}
		}
		private IReadOnlyCollection<IStateComponent> _components;

		[SerializeField, HideInInspector] private string id;
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] private Connections.State inConnection;
		[SerializeField, Output(backingValue = ShowBackingValue.Never, typeConstraint = TypeConstraint.Inherited)] private Connections.StateComponent components;

		protected override void Init()
		{
			EnsureUniqueId();
			base.Init();
		}

		protected void Awake()
		{
			Parent = GetInputNode<IState>(nameof(inConnection));
		}

		#region Callbacks

		/// <inheritdoc/>
		public override void OnEnteringState()
		{
			_components = GetComponents().ToList();
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
			Parent = parent;
		}

		public void SetDefaultChild(string id)
		{
			_defaultChild = id;
		}

		public void AddChild(IState child)
		{
			EnsureChildren();
			if (!_children.ContainsKey(child.ID))
			{
				_children.Add(child.ID, child);
			}
		}

		public void RemoveChild(string id)
		{
			EnsureChildren();
			if (_children.ContainsKey(id))
			{
				_children.Remove(id);
			}
		}

		private void EnsureChildren()
		{
			if (_children == null)
			{
				_children = GetChildren().ToDictionary((s) => s.ID);
			}
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
			return NodeExtensions.GetAllOutputNodes(this).Cast<IState>().Where((c) => c is not IState).ToHashSet();
		}
	}
}
