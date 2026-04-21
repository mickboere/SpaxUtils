using UnityEngine;

namespace SpaxUtils.StateMachines
{
	public class StateComponentNodeBase : StateMachineNodeBase
	{
		[SerializeField, NodeInput] protected Connections.StateComponent inConnection;
	}
}
