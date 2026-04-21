using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Abstract <see cref="GraphNodeBase"/> implementation for <see cref="StateMachines"/>.
	/// Implements <see cref="IStateListener"/>.
	/// </summary>
	public abstract class StateMachineNodeBase : GraphNodeBase, IStateListener
	{
		/// <summary>
		/// Name string used in the editor.
		/// </summary>
		public virtual string UserFacingName => string.Join(" ", new List<string>(GetType().Name.SplitCamelCase()).Where((n) => n != "Node"));

		/// <summary>
		/// The state currently responsible for invoking this node's callbacks.
		/// </summary>
		protected IState State { get; private set; }

		/// <inheritdoc/>
		public override void OnCreated()
		{
			base.OnCreated();
			name = UserFacingName;
		}

		#region Callbacks

		/// <inheritdoc/>
		public virtual void Initialize(IState state)
		{
			State = state;
		}

		/// <inheritdoc/>
		public virtual void OnEnteringState(ITransition transition) { }

		/// <inheritdoc/>
		public virtual void WhileEnteringState(ITransition transition) { }

		/// <inheritdoc/>
		public virtual void OnStateEntered() { }

		/// <inheritdoc/>
		public virtual void OnExitingState(ITransition transition) { }

		/// <inheritdoc/>
		public virtual void WhileExitingState(ITransition transition) { }

		/// <inheritdoc/>
		public virtual void OnStateExit() { }

		#endregion Callbacks

		public IReadOnlyCollection<IStateListener> GetComponents()
		{
			return GetAllOutputNodes().OfType<IStateListener>().Where(c => c is not IState).ToHashSet();
		}
	}
}
