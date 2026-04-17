using SpaxUtils.StateMachines;
using System;
using static UnityEngine.InputSystem.InputAction;

namespace SpaxUtils
{
	/// <summary>
	/// Helper class that maps player input to actor acts.
	/// </summary>
	public class PlayerInputToActMapper : InputToActMapper
	{
		private IAgent agent;
		private InputToActMapping mapping;
		private PlayerInputWrapper playerInputWrapper;
		private Option option;

		public PlayerInputToActMapper(IAgent agent, InputToActMapping mapping, PlayerInputWrapper playerInputWrapper) : base(agent.Actor, mapping)
		{
			this.agent = agent;
			this.mapping = mapping;
			this.playerInputWrapper = playerInputWrapper;

			option = new Option(mapping.Title, mapping.Input,
				delegate (CallbackContext c)
				{
					if (c.started)
					{
						Hold();
					}
					else if (c.canceled)
					{
						Release();
					}
				},
			playerInputWrapper, mapping.EatInput, mapping.InputPrio);

			agent.Brain.EnteredStateEvent += OnAgentStateChange;
			OnAgentStateChange(null);
		}

		public override void Dispose()
		{
			base.Dispose();

			agent.Brain.EnteredStateEvent -= OnAgentStateChange;

			Disable();
			option.Dispose();
		}

		private void OnAgentStateChange(IState state)
		{
			if (agent.Brain.IsStateActive(mapping.State))
			{
				Enable();
			}
			else
			{
				Disable();
			}
		}

		private void Enable()
		{
			if (!option.Enabled)
			{
				playerInputWrapper.RequestActionMaps(this, 0, mapping.ActionMap);
				option.Enable();
			}
		}

		private void Disable()
		{
			if (option.Enabled)
			{
				playerInputWrapper.CompleteActionMapRequest(this);
				option.Disable();
			}
		}
	}
}
