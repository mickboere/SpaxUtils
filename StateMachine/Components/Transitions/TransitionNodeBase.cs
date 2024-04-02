using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Base implementation of <see cref="IStateTransition"/>. Contains basic getters regarding state transitions.
	/// </summary>
	[NodeTint("#685535")]
	public abstract class TransitionNodeBase : StateMachineNodeBase, IStateTransition
	{
		public abstract bool Valid { get; }
		public abstract float Validity { get; }
		public virtual string NextState => GetOutputNode<FlowStateNode>(nameof(outConnection)).ID;
		public virtual float EntryProgress => 1f;
		public virtual float ExitProgress => 1f - EntryProgress;
		public virtual bool Completed => true;

		[SerializeField, Output(backingValue = ShowBackingValue.Never, connectionType = ConnectionType.Override, typeConstraint = TypeConstraint.Inherited)] protected Connections.State outConnection;

		public void Dispose()
		{
			throw new System.NotImplementedException();
		}
	}
}