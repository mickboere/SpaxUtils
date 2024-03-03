using System;
using static UnityEngine.InputSystem.InputAction;

namespace SpaxUtils
{
	/// <summary>
	/// Instanced helper class that maps player input to actor acts.
	/// </summary>
	public class InputToActMapper : IDisposable
	{
		private InputToActMapping mapping;
		private IAgent agent;
		private PlayerInputWrapper playerInputWrapper;

		private Option option;
		private bool holding;

		public InputToActMapper(InputToActMapping mapping, IAgent agent, PlayerInputWrapper playerInputWrapper)
		{
			this.mapping = mapping;
			this.agent = agent;
			this.playerInputWrapper = playerInputWrapper;

			playerInputWrapper.RequestActionMaps(this, 0, mapping.ActionMap);

			option = new Option(mapping.Title, mapping.Input,
				delegate (CallbackContext c)
				{
					if (c.started)
					{
						agent.Actor.Send(NewAct(true));
						holding = true;
					}
					else if (c.canceled)
					{
						agent.Actor.Send(NewAct(false));
						holding = false;
					}
				},
			playerInputWrapper, false, mapping.InputPrio);
			option.MakeAvailable();
		}

		public void Update()
		{
			if (mapping.HoldEveryFrame && holding)
			{
				agent.Actor.Send(NewAct(true));
			}
		}

		public void Dispose()
		{
			if (holding)
			{
				agent.Actor.Send(NewAct(false));
				holding = false;
			}

			option.Dispose();
			playerInputWrapper.CompleteActionMapRequest(this);
		}

		private Act<bool> NewAct(bool value)
		{
			return new Act<bool>(mapping, value);
		}
	}
}
