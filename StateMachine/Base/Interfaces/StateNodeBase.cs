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

		public IState ParentState => _parent ?? GetInputNode<IState>(nameof(inConnection));
		private IState _parent;

		public string DefaultChild => _defaultChild;
		public IState DefaultChildState => string.IsNullOrEmpty(_defaultChild) ? null : Children[_defaultChild];
		protected virtual string _defaultChild { get; private set; }

		public IReadOnlyDictionary<string, IState> Children => _children ?? GetAllChildStates().ToDictionary((s) => s.ID);
		private Dictionary<string, IState> _children;

		public IReadOnlyCollection<IStateListener> Components => _components ?? GetComponents();
		private IReadOnlyCollection<IStateListener> _components;

		[SerializeField, ReadOnly, HideInInspector] private string id;
		[SerializeField, NodeInput] private Connections.State inConnection;
		[SerializeField, NodeOutput(typeConstraint: TypeConstraint.Inherited)] private Connections.StateComponent components;

		private StateCallbackHelper callbackHelper;

		public override void OnCreated()
		{
			EnsureUniqueId();
			base.OnCreated();
		}

		public void InjectDependencies(IDependencyManager dependencyManager)
		{
			_parent = GetInputNode<IState>(nameof(inConnection));
			_children = GetAllChildStates().ToDictionary((s) => s.ID);
			_components = GetComponents().ToList();
			callbackHelper = new StateCallbackHelper(dependencyManager, this, _components);
			callbackHelper.Inject();
		}

		#region Callbacks

		/// <inheritdoc/>
		public override void OnEnteringState(ITransition transition)
		{
			base.OnEnteringState(transition);
			callbackHelper.OnEnteringState(transition);
		}

		/// <inheritdoc/>
		public override void WhileEnteringState(ITransition transition)
		{
			base.WhileEnteringState(transition);
			callbackHelper.WhileEnteringState(transition);
		}

		/// <inheritdoc/>
		public override void OnStateEntered()
		{
			base.OnStateEntered();
			Active = true;
			callbackHelper.OnStateEntered();
		}

		/// <inheritdoc/>
		public override void OnExitingState(ITransition transition)
		{
			base.OnExitingState(transition);
			callbackHelper.OnExitingState(transition);
		}

		/// <inheritdoc/>
		public override void WhileExitingState(ITransition transition)
		{
			base.WhileExitingState(transition);
			callbackHelper.WhileExitingState(transition);
		}

		/// <inheritdoc/>
		public override void OnStateExit()
		{
			base.OnStateExit();
			Active = false;
			callbackHelper.OnStateExit();
		}

		#endregion Callbacks

		#region IState implementation

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
			SpaxDebug.Error("Nodes are not dynamic.", "Could not add child.");
		}

		public void RemoveChild(string id)
		{
			SpaxDebug.Error("Nodes are not dynamic.", "Could not remove child.");
		}

		#endregion IState implementation

		private void EnsureUniqueId()
		{
			if (!Application.isPlaying)
			{
				if (string.IsNullOrEmpty(id))
				{
					id = System.Guid.NewGuid().ToString();
				}
				else if (Graph != null && Graph.Nodes.Any((node) => node is StateNodeBase state && state != this && state.id == id))
				{
					id = System.Guid.NewGuid().ToString();
				}
			}
		}

		public IReadOnlyCollection<IState> GetAllChildStates()
		{
			return GetAllOutputNodes().Where((c) => c is IState).Cast<IState>().ToHashSet();
		}
	}
}
