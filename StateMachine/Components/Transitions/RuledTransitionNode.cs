using UnityEngine;

namespace SpaxUtils.StateMachine
{
	/// <summary>
	/// Basic <see cref="RuledTransitionNodeBase"/> implementation, having an <see cref="Connections.StateComponent"/> in-connection.
	/// </summary>
	public class RuledTransitionNode : RuledTransitionNodeBase
	{
		public override string UserFacingName => "Transition";

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
	}
}