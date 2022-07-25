using UnityEngine;

namespace SpaxUtils.StateMachine
{
	/// <summary>
	/// Base implementation of <see cref="ITransitionComponent"/>. Contains basic getters regarding node transitions.
	/// </summary>
	[NodeTint("#685535")]
	public abstract class TransitionNodeBase : StateMachineNodeBase, ITransitionComponent
	{
		/// <inheritdoc/>
		public abstract bool Valid { get; }
		/// <inheritdoc/>
		public abstract float Validity { get; }

		[SerializeField, Output(backingValue = ShowBackingValue.Never, connectionType = ConnectionType.Override, typeConstraint = TypeConstraint.Inherited)] protected Connections.State outConnection;

		/// <inheritdoc/>
		public virtual FlowStateNode GetNextState()
		{
			return GetOutputNode<FlowStateNode>(nameof(outConnection));
		}
	}
}