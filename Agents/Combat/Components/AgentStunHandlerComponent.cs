using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentStunHandlerComponent : EntityComponentMono, IStunHandler
	{
		public event Action EnteredStunEvent;
		public event Action ExitedStunEvent;

		public bool Stunned { get; private set; }

		#region Tooltips
		private const string TT_RECOVERY_THRESH = "Upper velocity threshold below which Agent begins to recover.";
		private const string TT_RECOVERED_THRESH = "Lower velocity threshold below which control is fully returned to Agent.";
		#endregion Tooltips

		[Header("Grounded")]
		[SerializeField] private PoseSequenceBlendTree hitBlendTree;
		[SerializeField] private float minStunTime = 0.5f;
		[SerializeField, Tooltip(TT_RECOVERY_THRESH)] private float recoveryThreshold = 2f;
		[SerializeField, Tooltip(TT_RECOVERED_THRESH)] private float recoveredThreshold = 1f;
		[Header("Blasted")]
		[SerializeField] private float blastThreshold = 15f;
		[SerializeField] private AnimationClip airbornePoseClip;
		[SerializeField] private float minAirborneLength = 1.5f;
		[SerializeField] private AnimationClip flooredPoseClip;
		[SerializeField] private float getUpTime = 2f;
		[SerializeField] private AnimationClip fallPoseClip;
		[SerializeField] private float fallThreshold = 10f;
		[SerializeField] private AnimationClip crashPoseClip;
		[SerializeField] private float crashAngle = 35f;
		[SerializeField] private float crashStickTime = 3f;
		[SerializeField] private float crashDetectionRadius = 0.3f;
		[SerializeField] private LayerMask crashDetectionMask;

		private IAgent agent;
		private RigidbodyWrapper rigidbodyWrapper;
		private AnimatorPoser animatorPoser;
		private IAgentMovementHandler movementHandler;
		private AgentArmsComponent arms;
		private IGrounderComponent grounder;
		private CallbackService callbackService;

		private Pose airbornePose;
		private Pose flooredPose;
		private Pose crashPose;
		private Pose fallPose;

		private FloatOperationModifier controlMod;
		private FloatOperationModifier armsMod;
		private FloatOperationModifier gravityMod;

		private HitData stunHit;
		private TimerClass stunTimer;

		private bool blasted;
		private TimerClass airborneTimer;
		private TimerClass getUpTimer;
		private TimerClass crashTimer;
		private RaycastHit crashHit;

		public void InjectDependencies(IAgent agent, RigidbodyWrapper rigidbodyWrapper,
			AnimatorPoser animatorPoser, IAgentMovementHandler movementHandler,
			AgentArmsComponent arms, IGrounderComponent grounder, CallbackService callbackService)
		{
			this.agent = agent;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.animatorPoser = animatorPoser;
			this.movementHandler = movementHandler;
			this.arms = arms;
			this.grounder = grounder;
			this.callbackService = callbackService;
		}

		protected void Awake()
		{
			airbornePose = new Pose(airbornePoseClip);
			flooredPose = new Pose(flooredPoseClip);
			crashPose = new Pose(crashPoseClip);
			fallPose = new Pose(fallPoseClip);

			controlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			armsMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			gravityMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
		}

		protected void OnEnable()
		{
			rigidbodyWrapper.Control.AddModifier(this, controlMod);
			arms.Weight.AddModifier(this, armsMod);
			grounder.Gravity.AddModifier(this, gravityMod);
		}

		protected void OnDisable()
		{
			rigidbodyWrapper.Control.RemoveModifier(this);
			arms.Weight.RemoveModifier(this);
			grounder.Gravity.RemoveModifier(this);
		}

		protected void Update()
		{
			if (Stunned)
			{
				if (blasted)
				{
					UpdateBlastedStun();
				}
				else
				{
					UpdateGroundedStun();
				}
			}
		}

		public void EnterStun(HitData stunHit)
		{
			Stunned = true;
			this.stunHit = stunHit;
			stunTimer = new TimerClass(minStunTime, () => EntityTimeScale, callbackService);
			controlMod.SetValue(0f);
			agent.Actor.AddBlocker(this);
			getUpTimer?.Dispose();
			getUpTimer = null;
			crashTimer?.Dispose();
			crashTimer = null;

			if (blasted = rigidbodyWrapper.Speed > blastThreshold)
			{
				airborneTimer = new TimerClass(minAirborneLength, () => EntityTimeScale, callbackService);
			}

			EnteredStunEvent?.Invoke();
		}

		public void ExitStun()
		{
			Stunned = false;

			animatorPoser.RevokeInstructions(this);

			controlMod.SetValue(1f);
			armsMod.SetValue(1f);
			gravityMod.SetValue(1f);

			agent.Actor.RemoveBlocker(this);

			ExitedStunEvent?.Invoke();
		}

		private void UpdateGroundedStun()
		{
			float stunAmount = Mathf.Max(stunTimer.Progress.Invert(), Mathf.InverseLerp(recoveredThreshold, recoveryThreshold, rigidbodyWrapper.Speed));

			IPoserInstructions instructions = hitBlendTree.GetInstructions(0f, -stunHit.Direction.LocalizeDirection(rigidbodyWrapper.transform));
			animatorPoser.ProvideInstructions(this, PoserLayerConstants.BODY, instructions, 10, stunAmount);
			armsMod.SetValue(stunAmount.Invert());

			if (stunTimer.Expired && rigidbodyWrapper.Speed < recoveredThreshold)
			{
				ExitStun();
			}
		}

		private void UpdateBlastedStun()
		{
			float groundedAmount = Mathf.Lerp(0f, grounder.GroundedAmount, airborneTimer.Progress.InOutQuad());

			Vector3 direction = -rigidbodyWrapper.Velocity.MultY(groundedAmount.Invert()); // Apply terrain normal.

			//Debug.DrawLine(agent.Targetable.Center, agent.Targetable.Center + -direction.normalized * (crashDetectionRadius + direction.magnitude * Time.fixedDeltaTime), Color.red);
			if (crashTimer == null &&
				Physics.SphereCast(agent.Targetable.Center, crashDetectionRadius, -direction, out crashHit, crashDetectionRadius + direction.magnitude * Time.fixedDeltaTime, crashDetectionMask) &&
				Vector3.Angle(crashHit.normal, direction.normalized) < crashAngle)
			{
				crashTimer = new TimerClass(crashStickTime, () => EntityTimeScale, callbackService);
				rigidbodyWrapper.Velocity = Vector3.zero;
			}

			if (crashTimer != null)
			{
				gravityMod.SetValue(crashTimer.Progress.InOutCubic());
				direction = Vector3.Lerp(crashHit.normal, direction, crashTimer.Progress.InOutCubic());
			}

			movementHandler.ForceRotation(direction);

			PoseTransition blastedPose = new PoseTransition(airbornePose, flooredPose, groundedAmount);
			PoseTransition fallingPose = new PoseTransition(crashPose, fallPose, crashTimer != null ? crashTimer.Progress : 1f);
			float fallAmount = Mathf.Max(crashTimer != null ? crashTimer.Progress.Invert() : 0f, Mathf.InverseLerp(blastThreshold, fallThreshold, rigidbodyWrapper.Speed));
			float blend = grounder.Grounded && crashTimer == null ? 0f : fallAmount;
			PoserInstructions pose = new PoserInstructions(blastedPose, fallingPose, blend);

			float stunWeight = 1f;
			if (getUpTimer != null)
			{
				stunWeight *= getUpTimer.Progress.Invert();
			}
			else if (rigidbodyWrapper.Speed < recoveryThreshold)
			{
				getUpTimer = new TimerClass(getUpTime, () => EntityTimeScale, callbackService);
			}

			//SpaxDebug.Log("Blasted", $"crash={(crashTimer != null ? crashTimer.Time : 0f)}, ground={groundedAmount}, fall={fallAmount}, blend={blend}, weight={stunWeight}");

			animatorPoser.ProvideInstructions(this, PoserLayerConstants.BODY, pose, 10, stunWeight);
			armsMod.SetValue(stunWeight.Invert());

			if (getUpTimer != null && getUpTimer.Expired)
			{
				ExitStun();
			}
		}
	}
}
