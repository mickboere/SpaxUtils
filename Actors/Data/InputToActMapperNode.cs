using SpaxUtils.StateMachine;
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

		private List<InputToActMapper> mappers = new List<InputToActMapper>();

		public void InjectDependencies(PlayerInputWrapper playerInputWrapper, IAgent agent)
		{
			this.playerInputWrapper = playerInputWrapper;
			this.agent = agent;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			foreach (InputToActMapping mapping in map.Mappings)
			{
				mappers.Add(new InputToActMapper(mapping, agent, playerInputWrapper));
			}
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			foreach (InputToActMapper mapper in mappers)
			{
				mapper.Dispose();
			}
			mappers.Clear();
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			foreach (InputToActMapper mapper in mappers)
			{
				mapper.Update();
			}
		}
	}
}
