using System;
using System.Collections.Generic;
using System.Linq;
using XNode;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Abstract <see cref="Node"/> implementation for <see cref="StateMachines"/>.
	/// Implements <see cref="IStateListener"/>.
	/// </summary>
	public abstract class StateMachineNodeBase : Node, IStateListener
	{
		/// <summary>
		/// Name string used in the editor.
		/// </summary>
		public virtual string UserFacingName => string.Join(" ", new List<string>(GetType().Name.SplitCamelCase()).Where((n) => n != "Node"));

		#region Node Config

		public override object GetValue(NodePort port)
		{
			return null;
		}

		protected override void Init()
		{
			base.Init();
			name = UserFacingName;
		}

		#endregion Node Config

		#region Callbacks

		/// <inheritdoc/>
		public virtual void OnEnteringState() { }

		/// <inheritdoc/>
		public virtual void WhileEnteringState(ITransition transition) { }

		/// <inheritdoc/>
		public virtual void OnStateEntered() { }

		/// <inheritdoc/>
		public virtual void OnExitingState() { }

		/// <inheritdoc/>
		public virtual void WhileExitingState(ITransition transition) { }

		/// <inheritdoc/>
		public virtual void OnStateExit() { }

		#endregion Callbacks

		#region Connections

		/// <summary>
		/// Maps to <see cref="XNodeExtensions.GetInputNodes{T}(Node, string, Func{T, bool})"/>.
		/// </summary>
		public List<T> GetInputNodes<T>(string port, Func<T, bool> evaluation = null) where T : class
		{
			return XNodeExtensions.GetInputNodes<T>(this, port, evaluation);
		}

		/// <summary>
		/// Maps to <see cref="XNodeExtensions.GetInputNode{T}(Node, string){T}(StateMachineNodeBase, string)"/>.
		/// </summary>
		public T GetInputNode<T>(string port) where T : class
		{
			return XNodeExtensions.GetInputNode<T>(this, port);
		}

		/// <summary>
		/// Maps to <see cref="XNodeExtensions.GetOutputNodes{T}(Node, string, Func{T, bool})"/>.
		/// </summary>
		public List<T> GetOutputNodes<T>(string port, Func<T, bool> evaluation = null) where T : class
		{
			return XNodeExtensions.GetOutputNodes<T>(this, port, evaluation);
		}

		/// <summary>
		/// Maps to <see cref="XNodeExtensions.GetOutputNode{T}(Node, string)"/>.
		/// </summary>
		public T GetOutputNode<T>(string port) where T : class
		{
			return XNodeExtensions.GetOutputNode<T>(this, port);
		}

		#endregion Connections

		public IReadOnlyCollection<IStateListener> GetComponents()
		{
			return XNodeExtensions.GetAllOutputNodes(this).Cast<IStateListener>().Where((c) => c is not IState).ToHashSet();
		}
	}
}
