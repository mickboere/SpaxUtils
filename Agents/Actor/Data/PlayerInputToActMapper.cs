using System;
using static UnityEngine.InputSystem.InputAction;

namespace SpaxUtils
{
	/// <summary>
	/// Helper class that maps player input to actor acts.
	/// </summary>
	public class PlayerInputToActMapper : InputToActMapper
	{
		private PlayerInputWrapper playerInputWrapper;
		private Option option;

		public PlayerInputToActMapper(IAgent agent, InputToActMapping mapping, PlayerInputWrapper playerInputWrapper) : base(agent.Actor, mapping)
		{
			this.playerInputWrapper = playerInputWrapper;

			playerInputWrapper.RequestActionMaps(this, 0, mapping.ActionMap);

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
			option.Enable();
		}

		public override void Dispose()
		{
			base.Dispose();
			option.Dispose();
			playerInputWrapper.CompleteActionMapRequest(this);
		}
	}
}
