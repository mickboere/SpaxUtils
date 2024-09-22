using SpaxUtils.StateMachines;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class PlayerInputToActMapperNode : StateComponentNodeBase
	{
		private PlayerInputWrapper playerInputWrapper;
		private IAgent agent;
		private CallbackService callbackService;
		private InputToActMap map;

		private List<PlayerInputToActMapper> mappers = new List<PlayerInputToActMapper>();

		public void InjectDependencies(PlayerInputWrapper playerInputWrapper, IAgent agent, CallbackService callbackService, InputToActMap map)
		{
			this.playerInputWrapper = playerInputWrapper;
			this.agent = agent;
			this.callbackService = callbackService;
			this.map = map;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			foreach (InputToActMapping mapping in map.Mappings)
			{
				mappers.Add(new PlayerInputToActMapper(agent.Actor, mapping, playerInputWrapper));
			}

			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			foreach (PlayerInputToActMapper mapper in mappers)
			{
				mapper.Dispose();
			}
			mappers.Clear();

			callbackService.UnsubscribeUpdate(UpdateMode.Update, this);
		}

		private void OnUpdate(float delta)
		{
			foreach (PlayerInputToActMapper mapper in mappers)
			{
				mapper.Update();
			}
		}
	}
}
