using SpaxUtils.StateMachines;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class InputToActMapperNode : StateComponentNodeBase
	{
		[SerializeField] private PlayerInputToActMap map;

		private PlayerInputWrapper playerInputWrapper;
		private IAgent agent;
		private CallbackService callbackService;

		private List<InputToActMapper> mappers = new List<InputToActMapper>();

		public void InjectDependencies(PlayerInputWrapper playerInputWrapper, IAgent agent, CallbackService callbackService)
		{
			this.playerInputWrapper = playerInputWrapper;
			this.agent = agent;
			this.callbackService = callbackService;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			foreach (InputToActMapping mapping in map.Mappings)
			{
				mappers.Add(new InputToActMapper(mapping, agent, playerInputWrapper));
			}

			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			foreach (InputToActMapper mapper in mappers)
			{
				mapper.Dispose();
			}
			mappers.Clear();

			callbackService.UnsubscribeUpdate(UpdateMode.Update, this);
		}

		private void OnUpdate()
		{
			foreach (InputToActMapper mapper in mappers)
			{
				mapper.Update();
			}
		}
	}
}
