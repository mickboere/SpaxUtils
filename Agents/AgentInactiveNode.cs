using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	public class AgentInactiveNode : StateComponentNodeBase
	{
		private RigidbodyWrapper rigidbodyWrapper;
		private AnimatorPoser animatorPoser;

		public void InjectDependencies(RigidbodyWrapper rigidbodyWrapper,
			[Optional] AnimatorPoser animatorPoser)
		{
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.animatorPoser = animatorPoser;
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
