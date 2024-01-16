using SpaxUtils.StateMachine;
using System;
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
		[SerializeField] private bool interuptable;
		[SerializeField, HideInInspector] private bool customBuffer;
		[SerializeField, Conditional(nameof(customBuffer), drawToggle: true)] private float buffer;
		[SerializeField] private bool holdEveryFrame;

		private PlayerInputWrapper playerInputWrapper;
		private IAgent agent;
		private Option option;
		private bool holding;

		public void InjectDependencies(PlayerInputWrapper playerInputWrapper, IAgent agent)
		{
			this.playerInputWrapper = playerInputWrapper;
			this.agent = agent;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			playerInputWrapper.RequestActionMaps(this, 0, actionMap);
			option = new Option(act, input, (c) =>
			{
				if (c.started)
				{
					agent.Actor.Send(new Act<bool>(act, true, interuptable, customBuffer ? buffer : Act<bool>.DEFAULT_BUFFER));
					holding = true;
				}
				else if (c.canceled)
				{
					agent.Actor.Send(new Act<bool>(act, false, interuptable, customBuffer ? buffer : Act<bool>.DEFAULT_BUFFER));
					holding = false;
				}
			}, playerInputWrapper);
			option.MakeAvailable();
		}

		public override void OnExitingState(Action callback)
		{
			if (holding)
			{
				agent.Actor.Send(new Act<bool>(act, false));
				holding = false;
			}
			base.OnExitingState(callback);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			option.Dispose();
			playerInputWrapper.CompleteActionMapRequest(this);
		}

		public override void OnUpdate()
		{
			base.OnUpdate();
			if (holdEveryFrame && holding)
			{
				agent.Actor.Send(new Act<bool>(act, true, interuptable, customBuffer ? buffer : Act<bool>.DEFAULT_BUFFER));
			}
		}
	}
}
