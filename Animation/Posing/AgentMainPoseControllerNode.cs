using UnityEngine;
using SpaxUtils.StateMachines;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// Controls the agent's default poses.
	/// </summary>
	[NodeWidth(300)]
	public class AgentMainPoseControllerNode : StateMachineNodeBase
	{
		protected const float POSE_SNAPPING_TRESHOLD = 0.01f;

		protected Poser Poser => agentPoser.GetMainPoser(PoserLayerConstants.BODY);

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField] private PoseSequenceBlendTree movementBlendTree;

		private RigidbodyWrapper rigidbodyWrapper;
		private AnimatorPoser agentPoser;
		private IAgentMovementHandler agentMovementHandler;
		private CallbackService callbackService;

		private float fromProgress;
		private float toProgress;

		public void InjectDependencies(RigidbodyWrapper rigidbodyWrapper, AnimatorPoser agentPoser,
			IAgentMovementHandler agentMovementHandler, CallbackService callbackService)
		{
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.agentPoser = agentPoser;
			this.agentMovementHandler = agentMovementHandler;
			this.callbackService = callbackService;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			callbackService.UnsubscribeUpdate(UpdateMode.Update, this);
		}

		private void OnUpdate()
		{
			Vector3 blendPosition = rigidbodyWrapper.RelativeVelocity / agentMovementHandler.MovementSpeed * rigidbodyWrapper.Grip;
			(IPoseSequence from, IPoseSequence to, float interpolation) instructions = movementBlendTree.GetPoseBlend(blendPosition);

			if (instructions.from == null)
			{
				return;
			}

			if (instructions.interpolation > 1f - POSE_SNAPPING_TRESHOLD)
			{
				fromProgress = toProgress;
			}
			else if (instructions.interpolation < POSE_SNAPPING_TRESHOLD)
			{
				toProgress = fromProgress;
			}

			PoseTransition from = instructions.from.Evaluate(fromProgress * instructions.from.TotalDuration);
			PoseTransition to = instructions.to.Evaluate(toProgress * instructions.to.TotalDuration);

			from.TryEvaluateFloat(AnimationFloatConstants.STEP_INTERVAL, 0f, out float fromStepInterval);
			to.TryEvaluateFloat(AnimationFloatConstants.STEP_INTERVAL, 0f, out float toStepInterval);
			from.TryEvaluateFloat(AnimationFloatConstants.STEP_SIZE, 0f, out float fromStepSize);
			to.TryEvaluateFloat(AnimationFloatConstants.STEP_SIZE, 0f, out float toStepSize);

			float fromChange = fromStepInterval > 0f ?
				rigidbodyWrapper.Speed / fromStepInterval / fromStepSize * Time.deltaTime :
				Time.deltaTime / instructions.from.TotalDuration;
			float toChange = toStepInterval > 0f ?
				rigidbodyWrapper.Speed / toStepInterval / toStepSize * Time.deltaTime :
				Time.deltaTime / instructions.to.TotalDuration;

			if (Mathf.Approximately(fromStepInterval, toStepInterval))
			{
				// Sync animations of equal interval that are blending.
				float progress = Mathf.Lerp(fromChange, toChange, instructions.interpolation);
				fromProgress += progress;
				toProgress += progress;
			}
			else
			{
				fromProgress += fromChange;
				toProgress += toChange;
			}

			//SpaxDebug.Log($"Pose, input={rigidbodyWrapper.RelativeVelocity / agentMovementHandler.MovementSpeed}, ", instructions.ToString());

			Poser.Pose(from, to, instructions.interpolation);
		}
	}
}
