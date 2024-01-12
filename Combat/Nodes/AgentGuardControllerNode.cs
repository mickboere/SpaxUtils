using SpaxUtils.StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentGuardControllerNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;

		private IAgent agent;
		private IActor actor;
		private AnimatorPoser poser;
		private IAgentMovementHandler movementHandler;
		private RigidbodyWrapper rigidbodyWrapper;
		private GuardPerformerComponent guardPerformerComponent;

		public void InjectDependencies(IAgent agent, IActor actor, AnimatorPoser poser,
			IAgentMovementHandler movementHandler, RigidbodyWrapper rigidbodyWrapper,
			GuardPerformerComponent guardPerformerComponent)
		{
			this.agent = agent;
			this.actor = actor;
			this.poser = poser;
			this.movementHandler = movementHandler;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.guardPerformerComponent = guardPerformerComponent;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			guardPerformerComponent.PoseUpdateEvent += OnPoseUpdateEvent;
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			guardPerformerComponent.PoseUpdateEvent -= OnPoseUpdateEvent;
			poser.RevokeInstructions(this);
		}

		private void OnPoseUpdateEvent(PoserStruct pose, float weight)
		{
			poser.ProvideInstructions(this, PoserLayerConstants.BODY, pose, 1, weight);
		}
	}
}
