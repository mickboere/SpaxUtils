using UnityEngine;

namespace SpaxUtils.StateMachine
{
	public class StateComponentNodeBase : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] private Connections.StateComponent inConnection;
	}
}
