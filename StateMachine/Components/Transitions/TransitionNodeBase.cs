using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Base implementation of <see cref="IStateTransition"/>. Contains basic getters regarding node transitions.
	/// </summary>
	[NodeTint("#685535")]
	public abstract class TransitionNodeBase : StateMachineNodeBase, IStateTransition
	{
		/// <inheritdoc/>
		public abstract bool Valid { get; }
		/// <inheritdoc/>
		public abstract float Validity { get; }

		public string NextState => throw new System.NotImplementedException();

		public float EntryProgress => throw new System.NotImplementedException();

		public float ExitProgress => throw new System.NotImplementedException();

		public bool Completed => throw new System.NotImplementedException();

		[SerializeField, Output(backingValue = ShowBackingValue.Never, connectionType = ConnectionType.Override, typeConstraint = TypeConstraint.Inherited)] protected Connections.State outConnection;

		public FlowStateNode GetNextState()
		{
			return GetOutputNode<FlowStateNode>(nameof(outConnection));
		}

		public void Dispose()
		{
			throw new System.NotImplementedException();
		}
	}
}