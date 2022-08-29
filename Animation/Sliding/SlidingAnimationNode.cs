using SpaxUtils.StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class SlidingAnimationNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField] private PoseSequenceBlendTree slidingBlendTree;

		private RigidbodyWrapper rigidbodyWrapper;
		private IGrounderComponent grounder;
		private AnimatorPoser agentPoser;
		private IAgentMovementHandler agentMovementHandler;
		private SurveyorComponent legWalkerComponent;
		private CallbackService callbackService;

		public void InjectDependencies(RigidbodyWrapper rigidbodyWrapper, IGrounderComponent grounder, AnimatorPoser agentPoser,
			IAgentMovementHandler agentMovementHandler, SurveyorComponent legWalkerComponent, CallbackService callbackService)
		{
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.grounder = grounder;
			this.agentPoser = agentPoser;
			this.agentMovementHandler = agentMovementHandler;
			this.legWalkerComponent = legWalkerComponent;
			this.callbackService = callbackService;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

		}
	}
}
