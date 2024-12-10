using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	public class AgentInactiveNode : StateComponentNodeBase
	{
		private IAgent agent;
		private RigidbodyWrapper rigidbodyWrapper;

		public void InjectDependencies(IAgent agent, RigidbodyWrapper rigidbodyWrapper)
		{
			this.agent = agent;
			this.rigidbodyWrapper = rigidbodyWrapper;
		}

		public override void OnEnteringState(ITransition transition)
		{
			base.OnEnteringState(transition);
			rigidbodyWrapper.IsKinematic = true;
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			rigidbodyWrapper.IsKinematic = false;
		}
	}
}
