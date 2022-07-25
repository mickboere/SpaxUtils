using SpaxUtils.StateMachine;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Sets the Agent's current target to null.
	/// </summary>
	public class AgentStopTargettingNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;

		private ITargeter targeter;

		public void InjectDependencies(ITargeter targetter)
		{
			this.targeter = targetter;
		}
		public override void OnStateEntered()
		{
			base.OnStateEntered();

			targeter.StopTargetting();
		}
	}
}
