using SpaxUtils.StateMachine;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaxUtils
{
	/// <summary>
	/// Class that listens for user-input in order to progress the brain state.
	/// </summary>
	[NodeTint("#eb34e8"), NodeWidth(225)]
	public class BrainStateInputControllerNode : StateMachineNodeBase
	{
		public override string UserFacingName => $"[{string.Join(",", actions)}] > {nextState}";

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] private Connections.StateComponent inConnection;
		[SerializeField, ConstDropdown(typeof(IInputActionMaps))] private string actionMap;
		[SerializeField, ConstDropdown(typeof(IInputActions))] private List<string> actions;
		[SerializeField] private InputActionPhase phase;
		[SerializeField, ConstDropdown(typeof(IStateIdentifierConstants))] private string nextState;

		private Brain brain;
		private PlayerInputWrapper playerInputWrapper;
		private Dictionary<object, Option> options = new Dictionary<object, Option>();

		public void InjectDependencies(Brain brain, PlayerInputWrapper playerInputWrapper)
		{
			this.brain = brain;
			this.playerInputWrapper = playerInputWrapper;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			playerInputWrapper.RequestActionMaps(this, 0, actionMap);
			foreach (string action in actions)
			{
				options[action] = new Option(nextState, action, OnInputReceived, playerInputWrapper);
				options[action].MakeAvailable();
			}
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			foreach (KeyValuePair<object, Option> kvp in options)
			{
				kvp.Value.Dispose();
			}
			options.Clear();
			playerInputWrapper.CompleteActionMapRequest(this);
		}

		private void OnInputReceived(InputAction.CallbackContext context)
		{
			if (context.phase == phase)
			{
				brain.EnterState(nextState);
			}
		}
	}
}
