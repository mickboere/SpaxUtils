using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Basic <see cref="ConditionNodeBase"/> implementation, having an <see cref="Connections.StateComponent"/> in-connection.
	/// </summary>
	public class ConditionNode : ConditionNodeBase
	{
		public override string UserFacingName => "Condition";

		public override string NextState => _elseState == null || Valid ? base.NextState : _elseState.ID;
		private IState _elseState;

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField, Output(backingValue = ShowBackingValue.Never, connectionType = ConnectionType.Override, typeConstraint = TypeConstraint.Inherited)] protected Connections.State elseConnection;

		public void InjectDependencies()
		{
			_elseState = GetOutputNode<IState>(nameof(outConnection));
		}
	}
}