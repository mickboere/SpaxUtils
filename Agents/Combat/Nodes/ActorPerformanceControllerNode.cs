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
	public class ActorPerformanceControllerNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;

		private IActor actor;

		public void InjectDependencies(IActor actor)
		{
			this.actor = actor;
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			// Force cancel current performance(s).
			actor.TryCancel(true);
		}
	}
}
