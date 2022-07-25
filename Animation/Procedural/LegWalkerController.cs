using SpaxUtils.StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class LegWalkerController : StateMachineNodeBase
	{
		protected Poser Poser => agentPoser.GetMainPoser(PoserLayerConstants.BODY);

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField] private PoseSequenceBlendTree movementBlendTree;

		private RigidbodyWrapper rb;
		private AnimatorPoser agentPoser;
		private IAgentMovementHandler agentMovementHandler;
		private LegWalkerComponent legWalkerComponent;
		private CallbackService callbackService;
		private ILegsComponent legs;

		public void InjectDependencies(RigidbodyWrapper rb, AnimatorPoser agentPoser,
			IAgentMovementHandler agentMovementHandler, LegWalkerComponent legWalkerComponent,
			CallbackService callbackService, ILegsComponent legs)
		{
			this.rb = rb;
			this.agentPoser = agentPoser;
			this.agentMovementHandler = agentMovementHandler;
			this.legWalkerComponent = legWalkerComponent;
			this.callbackService = callbackService;
			this.legs = legs;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			callbackService.FixedUpdateCallback += OnFixedUpdateCallback;
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			callbackService.FixedUpdateCallback -= OnFixedUpdateCallback;
			legWalkerComponent.ResetSurveyor();
		}

		private void UpdatePose()
		{
			Vector3 blendPosition = rb.RelativeVelocity / agentMovementHandler.MovementSpeed * rb.Grip;
			(IPoseSequence from, IPoseSequence to, float interpolation) instructions = movementBlendTree.GetInstructions(blendPosition);

			if (instructions.from == null)
			{
				return;
			}

			instructions.from.GlobalData.TryGetFloat(AnimationFloatConstants.CYCLE_OFFSET, 0f, out float fromOffset);
			instructions.to.GlobalData.TryGetFloat(AnimationFloatConstants.CYCLE_OFFSET, 0f, out float toOffset);
			float fromProgress = legWalkerComponent.GetProgress(fromOffset * (1f - instructions.interpolation), false);
			float toProgress = legWalkerComponent.GetProgress(toOffset * instructions.interpolation, false);
			PoseTransition from = instructions.from.Evaluate(fromProgress * instructions.from.TotalDuration);
			PoseTransition to = instructions.to.Evaluate(toProgress * instructions.to.TotalDuration);
			Poser.Pose(from, to, instructions.interpolation);
		}

		private void OnFixedUpdateCallback()
		{
			legWalkerComponent.UpdateSurveyor(Time.fixedDeltaTime);
			UpdatePose();
		}
	}
}
