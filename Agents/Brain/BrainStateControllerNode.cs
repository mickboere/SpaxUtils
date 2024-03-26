using SpaxUtils.StateMachines;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Class that listens for a <see cref="NextStateMsg"/> to progress the brain state.
	/// Intented to make brain graphs more readable by exposing default transitions.
	/// </summary>
	[NodeTint("#eb34e8"), NodeWidth(225)]
	public class BrainStateControllerNode : StateMachineNodeBase
	{
		public override string UserFacingName => $"Next: {nextState}";

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] private Connections.StateComponent inConnection;
		[SerializeField, ConstDropdown(typeof(IStateIdentifierConstants))] private string nextState;

		private Brain brain;
		private ICommunicationChannel comms;

		public void InjectDependencies(Brain brain, ICommunicationChannel comms)
		{
			this.brain = brain;
			this.comms = comms;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			comms.Listen<NextStateMsg>(this, OnNextState);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			comms.StopListening(this);
		}

		private void OnNextState(NextStateMsg msg)
		{
			brain.TryTransition(nextState);
		}
	}
}
