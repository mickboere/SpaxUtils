using System.Collections.Generic;
using System.Linq;
using XNode;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Abstract <see cref="Node"/> implementation for <see cref="StateMachines"/>.
	/// Implements <see cref="IStateComponent"/>.
	/// </summary>
	public abstract class StateMachineNodeBase : Node, IStateComponent
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
		public virtual void OnPrepare() { }

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
		/// Maps to <see cref="NodeExtensions.GetInputNodes{T}(Node, string){T}(StateMachineNodeBase, string)"/>.
		/// </summary>
		public List<T> GetInputNodes<T>(string port) where T : class
		{
			return NodeExtensions.GetInputNodes<T>(this, port);
		}

		/// <summary>
		/// Maps to <see cref="NodeExtensions.GetInputNode{T}(Node, string){T}(StateMachineNodeBase, string)"/>.
		/// </summary>
		public T GetInputNode<T>(string port) where T : class
		{
			return NodeExtensions.GetInputNode<T>(this, port);
		}

		/// <summary>
		/// Maps to <see cref="NodeExtensions.GetOutputNodes{T}(StateMachineNodeBase, string)"/>.
		/// </summary>
		public List<T> GetOutputNodes<T>(string port) where T : class
		{
			return NodeExtensions.GetOutputNodes<T>(this, port);
		}

		/// <summary>
		/// Maps to <see cref="NodeExtensions.GetOutputNode{T}(StateMachineNodeBase, string)"/>.
		/// </summary>
		public T GetOutputNode<T>(string port) where T : class
		{
			return NodeExtensions.GetOutputNode<T>(this, port);
		}

		#endregion Connections

		public IReadOnlyCollection<IStateComponent> GetComponents()
		{
			return NodeExtensions.GetAllOutputNodes(this).Cast<IStateComponent>().Where((c) => c is not IState).ToHashSet();
		}
	}
}
