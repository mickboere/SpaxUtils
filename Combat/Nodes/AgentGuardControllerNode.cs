using SpaxUtils.StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentGuardControllerNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField] private float controlWeightSmoothing = 6f;

		private IAgent agent;
		private IActor actor;
		private AnimatorPoser poser;
		private IAgentMovementHandler movementHandler;
		private RigidbodyWrapper rigidbodyWrapper;
		private GuardPerformerComponent guardPerformerComponent;
		private AgentArmsComponent arms;

		private FloatOperationModifier controlMod;

		public void InjectDependencies(IAgent agent, IActor actor, AnimatorPoser poser,
			IAgentMovementHandler movementHandler, RigidbodyWrapper rigidbodyWrapper,
			GuardPerformerComponent guardPerformerComponent, AgentArmsComponent arms)
		{
			this.agent = agent;
			this.actor = actor;
			this.poser = poser;
			this.movementHandler = movementHandler;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.guardPerformerComponent = guardPerformerComponent;
			this.arms = arms;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			guardPerformerComponent.PoseUpdateEvent += OnPoseUpdateEvent;
			guardPerformerComponent.PerformanceCompletedEvent += OnGuardCompleteEvent;

			controlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			rigidbodyWrapper.Control.AddModifier(this, controlMod);
			arms.Weight.AddModifier(this, controlMod);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			guardPerformerComponent.PoseUpdateEvent -= OnPoseUpdateEvent;
			guardPerformerComponent.PerformanceCompletedEvent -= OnGuardCompleteEvent;

			rigidbodyWrapper.Control.RemoveModifier(this);
			arms.Weight.RemoveModifier(this);
			controlMod.Dispose();

			poser.RevokeInstructions(this);
		}

		private void OnPoseUpdateEvent(PoserStruct pose, float weight)
		{
			poser.ProvideInstructions(this, PoserLayerConstants.BODY, pose, 1, weight);

			float control = 1f - weight;
			controlMod.SetValue(controlMod.Value < control ? Mathf.Lerp(controlMod.Value, control, controlWeightSmoothing * Time.deltaTime) : control);
		}

		private void OnGuardCompleteEvent(IPerformer performer)
		{
			controlMod.SetValue(1f);
		}
	}
}
