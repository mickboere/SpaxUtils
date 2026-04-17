using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Basic <see cref="ConditionNodeBase"/> implementation, having an <see cref="Connections.StateComponent"/> in-connection.
	/// </summary>
	public class ConditionNode : ConditionNodeBase
	{
		public override string UserFacingName => "Condition";

		public override bool Valid => _elseState != null || _valid;
		public override string NextState
		{
			get
			{
				if (_elseState == null)
				{
					// Has no else state.
					return base.NextState;
				}
				else
				{
					// Has else state.
					return _valid ? base.NextState : _elseState.ID;
				}
			}
		}
		private IState _elseState;

		public override float Validity => priority <= 0f ? base.Validity : base.Validity * priority;

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField, Output(backingValue = ShowBackingValue.Never, connectionType = ConnectionType.Override, typeConstraint = TypeConstraint.Inherited)] protected Connections.State elseConnection;
		[SerializeField, Tooltip(">0 Multiplies the validity of this transition.")] private float priority = 0f;

		public void InjectDependencies()
		{
			_elseState = GetOutputNode<IState>(nameof(elseConnection));
		}
	}
}