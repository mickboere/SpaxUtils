using SpaxUtils.StateMachine;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaxUtils
{
	/// <summary>
	/// Class that listens for user-input in order to progress the brain state.
	/// </summary>
	[NodeTint("#3d6cd9"), NodeWidth(225)]
	public class InputToActionControllerNode : StateMachineNodeBase
	{
		public override string UserFacingName => $"[{input}] => <{act}>";

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] private Connections.StateComponent inConnection;
		[SerializeField, ConstDropdown(typeof(IInputActionMaps))] private string actionMap;
		[SerializeField, ConstDropdown(typeof(IInputActions))] private string input;
		[SerializeField, ConstDropdown(typeof(IActConstants))] private string act;

		private PlayerInputWrapper playerInputWrapper;
		private IAgent agent;
		private Option option;

		public void InjectDependencies(PlayerInputWrapper playerInputWrapper, IAgent agent)
		{
			this.playerInputWrapper = playerInputWrapper;
			this.agent = agent;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			playerInputWrapper.RequestActionMaps(this, 0, actionMap);
			// Create option that maps input press to act<true>, release to act<false>.
			option = new Option(act, input, (c) =>
			{
				if (c.started)
				{
					agent.Actor.Send(new Act<bool>(act, true));
				}
				else if (c.canceled)
				{
					agent.Actor.Send(new Act<bool>(act, false));
				}
			}, playerInputWrapper);
			option.MakeAvailable();
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			option.Dispose();
			playerInputWrapper.CompleteActionMapRequest(this);
		}
	}
}
