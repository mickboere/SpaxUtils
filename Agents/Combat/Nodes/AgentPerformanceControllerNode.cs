using System;
using System.Collections.Generic;
using System.Linq;
using SpaxUtils.StateMachines;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Node that controls pose performance of the <see cref="MovePerformerComponent"/>.
	/// </summary>
	public class AgentPerformanceControllerNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;

		private IAgent agent;

		public void InjectDependencies(IAgent agent)
		{
			this.agent = agent;
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			// Force cancel current performance(s).
			agent.Actor.TryCancel(true);
		}
	}
}
