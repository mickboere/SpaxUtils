using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Basic <see cref="ConditionNodeBase"/> implementation, having an <see cref="Connections.StateComponent"/> in-connection.
	/// </summary>
	public class ConditionNode : ConditionNodeBase
	{
		public override string UserFacingName => "Condition";

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
	}
}