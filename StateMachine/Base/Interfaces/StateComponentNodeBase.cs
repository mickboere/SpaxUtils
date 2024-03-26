using UnityEngine;

namespace SpaxUtils.StateMachines
{
	public class StateComponentNodeBase : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
	}
}
