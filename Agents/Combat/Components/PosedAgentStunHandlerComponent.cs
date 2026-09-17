using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[DefaultExecutionOrder(100)]
	public class PosedAgentStunHandlerComponent : AgentStunHandlerComponent
	{
		#region Tooltips
		private const string TT_RECOVERY_THRESH = "Grounded speed below which recovery starts. Recovery lasts the predicted braking time from that speed.";
		#endregion Tooltips

		protected override bool DefaultExitBehavior => false;
		protected bool Debug => debug && Entity.RuntimeData.GetValue<bool>(EntityDataIdentifiers.DEBUG, true);

		[Header("Grounded")]
		[SerializeField] private PoseBlendMap hitBlendTree;
		[SerializeField, Tooltip(TT_RECOVERY_THRESH)] protected float recoveryThreshold = 3f;

		[Header("Flying")]
		[SerializeField] private float horizontalFlyThreshold = 15f;
		[SerializeField] private float verticalFlyThreshold = 1f;
		[SerializeField] private AnimationClip airbornePoseClip;
		[SerializeField] private AnimationClip flooredPoseClip;
		[SerializeField] private AnimationClip fallPoseClip;
		[SerializeField] private float fallThreshold = 10f;
		[SerializeField] private AnimationClip crashPoseClip;
		[SerializeField] private float crashAngle = 35f;
		[SerializeField] private float crashStickTime = 3f;
		[SerializeField] private float crashDetectionRadius = 0.3f;
		[SerializeField] private LayerMask crashDetectionMask;

		[Header("Pose Smoothing")]
		[SerializeField, Range(0f, 100f)] private float groundedAmountSmoothing = 30f;

		[Header("Debugging")]
		[SerializeField] private bool debug;

		private AnimatorPoser animatorPoser;
		private IAgentMovementHandler movementHandler;
		private AgentArmsComponent arms;
		private GrounderComponent grounder;

		private Pose airbornePose;
		private Pose flooredPose;
		private Pose crashPose;
		private Pose fallPose;

		private FloatOperationModifier armsMod;
		private FloatOperationModifier gravityMod;

		// Low anchor (fraction of horizontalFlyThreshold) where the horizontal launch pose starts ramping in.
		private const float HorizontalPoseSoftness = 0.75f;

		private bool flying;
		private float smoothedFlyingAmount;
		private TimerClass recoveryTimer;
		private TimerClass crashTimer;
		private RaycastHit crashHit;

		public void InjectDependencies(AnimatorPoser animatorPoser, IAgentMovementHandler movementHandler,
			AgentArmsComponent arms, GrounderComponent grounder)
		{
			this.animatorPoser = animatorPoser;
			this.movementHandler = movementHandler;
			this.arms = arms;
			this.grounder = grounder;
		}

		protected override void Awake()
		{
			base.Awake();

			airbornePose = new Pose(airbornePoseClip);
			flooredPose = new Pose(flooredPoseClip);
			crashPose = new Pose(crashPoseClip);
			fallPose = new Pose(fallPoseClip);

			armsMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			gravityMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			arms.Weight.AddModifier(this, armsMod);
			grounder.Gravity.AddModifier(this, gravityMod);
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			arms.Weight.RemoveModifier(this);
			grounder.Gravity.RemoveModifier(this);
		}

		protected override void FixedUpdate()
		{
			base.FixedUpdate();

			if (!Stunned)
			{
				return;
			}

			float horizontalSpeed = rigidbodyWrapper.Velocity.FlattenY().magnitude;

			// One unified "flying amount": how far off the ground we are, OR how hard we're launched horizontally.
			// Rotation and the flying pose are both driven from this, so they can never fall out of sync.
			float airborneAmount = grounder != null ? grounder.GroundedAmount.Invert() : 1f;
			float launchedAmount = Mathf.InverseLerp(horizontalFlyThreshold * HorizontalPoseSoftness, horizontalFlyThreshold, horizontalSpeed);
			float flyingAmount = Mathf.Max(airborneAmount, launchedAmount);

			// Smooth so the pose does not snap when grounding/velocity flickers.
			if (groundedAmountSmoothing <= 0f)
			{
				smoothedFlyingAmount = flyingAmount;
			}
			else
			{
				smoothedFlyingAmount = Mathf.MoveTowards(
					smoothedFlyingAmount,
					flyingAmount,
					groundedAmountSmoothing * Time.fixedDeltaTime);
			}

			// Decide if we should enter/keep flying mode.
			bool shouldFly =
				(grounder != null && !grounder.Grounded) ||
				horizontalSpeed > horizontalFlyThreshold ||
				rigidbodyWrapper.Velocity.y > verticalFlyThreshold;

			flying = flying || shouldFly;

			// Latch once, so regained movement can't hold us above the threshold. Planted decel is constant: t = 2d/v.
			float speed = rigidbodyWrapper.PredictedVelocity.magnitude;
			if (recoveryTimer == null && speed < recoveryThreshold && (grounder == null || grounder.Grounded))
			{
				float time = speed > 0.0001f ? 2f * movementHandler.PredictBrakingDistance(speed) / speed : 0f;
				recoveryTimer = new TimerClass(Mathf.Max(time, 0.0001f), () => EntityTimeScale, callbackService, UpdateMode.FixedUpdate);
			}

			UpdateGroundedStun();

			if (flying)
			{
				UpdateFlyingStun();
			}

			// Unblock at recovery; performers gate themselves on the returning Control.
			if (recoveryTimer != null && stunTimer.Expired)
			{
				Agent.Actor.RemoveBlocker(this);
				if (recoveryTimer.Expired)
				{
					ExitStun();
				}
			}
		}

		public override void EnterStun(HitData stunHit, float duration = -1f)
		{
			base.EnterStun(stunHit, duration);

			flying = false;
			CleanTimers();

			smoothedFlyingAmount = grounder != null ? grounder.GroundedAmount.Invert() : 1f;

			if (Debug)
			{
				SpaxDebug.Log($"EnterStun [{duration}s]", $"V={rigidbodyWrapper.Velocity}\n{stunHit}");
			}
		}

		public override void ExitStun()
		{
			base.ExitStun();

			animatorPoser.RevokeInstructions(hitBlendTree);
			animatorPoser.RevokeInstructions(airbornePose);

			armsMod.SetValue(1f);
			gravityMod.SetValue(1f);

			CleanTimers();

			if (Debug)
			{
				SpaxDebug.Log("ExitStun", stunHit != null ? stunHit.ToString() : "NULL");
			}
		}

		private void UpdateGroundedStun()
		{
			// OutQuad of the remaining time mirrors the old speed ramp under constant deceleration.
			float stunAmount =
				Mathf.Max(
					stunTimer.Progress.InvertClamped().InOutExpo(),
					recoveryTimer != null ? recoveryTimer.Progress.InvertClamped().OutQuad() : 1f);

			IPoserInstructions instructions =
				hitBlendTree.GetInstructions(0f, -stunHit.Direction.LocalizeDirection(rigidbodyWrapper.transform));

			animatorPoser.ProvideInstructions(hitBlendTree, PoserLayerConstants.BODY, instructions, 10, stunAmount);
			armsMod.SetValue(stunAmount.Invert());
			controlMod.SetValue(stunAmount.Invert());

			if (Debug)
			{
				SpaxDebug.Log("Stun: Grounded",
					"velocity=" + rigidbodyWrapper.Velocity +
					" crashTimer=" + (crashTimer != null ? crashTimer.Time.ToString("0.###") : "NULL") +
					" recoveryTimer=" + (recoveryTimer != null ? recoveryTimer.Time.ToString("0.###") : "NULL") +
					"\nstunAmount=" + stunAmount.ToString("0.###"));
			}
		}

		private void UpdateFlyingStun()
		{
			// "How grounded" for pose and direction purposes — the inverse of our unified flying amount.
			float groundedAmount = smoothedFlyingAmount.Invert();

			// Flatten the vertical direction while grounded so a small upward pop can't pitch the body into the floor.
			Vector3 direction = -rigidbodyWrapper.Velocity.MultY(groundedAmount.Invert());

			if (Debug)
			{
				UnityEngine.Debug.DrawLine(
					Agent.Targetable.Center,
					Agent.Targetable.Center + -direction.normalized * (crashDetectionRadius + direction.magnitude * Time.fixedDeltaTime),
					Color.red);
			}

			// Crash detection.
			if (crashTimer == null &&
				Physics.SphereCast(
					Agent.Targetable.Center,
					crashDetectionRadius,
					-direction,
					out crashHit,
					crashDetectionRadius + direction.magnitude * Time.fixedDeltaTime,
					crashDetectionMask) &&
				Vector3.Angle(crashHit.normal, direction.normalized) < crashAngle)
			{
				if (Debug)
				{
					SpaxDebug.Log("Stun: Crashed.");
				}

				crashTimer = new TimerClass(crashStickTime, () => EntityTimeScale, callbackService, UpdateMode.FixedUpdate);
				rigidbodyWrapper.ResetVelocity();
			}

			if (crashTimer != null)
			{
				float crashProg = crashTimer.Progress.Clamp01().InOutCubic();
				gravityMod.SetValue(crashProg);
				direction = Vector3.Lerp(crashHit.normal, direction, crashProg);
			}

			// Rotate toward the launch direction only as far as we're actually flying — matches the pose weight below.
			movementHandler.ForceRotation(direction, smoothedFlyingAmount);

			// Pose construction.
			PoseTransition blastedPose = new PoseTransition(airbornePose, flooredPose, groundedAmount);
			PoseTransition fallingPose = new PoseTransition(crashPose, fallPose, crashTimer != null ? crashTimer.Progress.Clamp01() : 1f);

			float fallAmount =
				Mathf.Max(
					crashTimer != null ? crashTimer.Progress.InvertClamped() : 0f,
					Mathf.InverseLerp(horizontalFlyThreshold, fallThreshold, rigidbodyWrapper.Speed));

			float blend = (grounder != null && grounder.Grounded && crashTimer == null) ? 0f : fallAmount;
			PoserInstructions pose = new PoserInstructions(blastedPose, fallingPose, blend);

			// Get up over the recovery timer.
			float stunWeight = recoveryTimer != null ? recoveryTimer.Progress.InvertClamped() : 1f;

			if (Debug)
			{
				SpaxDebug.Log("Stun: Flying",
					"velocity=" + rigidbodyWrapper.Velocity +
					" crashTimer=" + (crashTimer != null ? crashTimer.Time.ToString("0.###") : "NULL") +
					" recoveryTimer=" + (recoveryTimer != null ? recoveryTimer.Time.ToString("0.###") : "NULL") +
					"\nrawGround=" + (grounder != null ? grounder.GroundedAmount.ToString("0.###") : "NULL") +
					" fly=" + smoothedFlyingAmount.ToString("0.###") +
					" grounded=" + groundedAmount.ToString("0.###") +
					" fall=" + fallAmount.ToString("0.###") +
					" blend=" + blend.ToString("0.###") +
					" gravMod=" + gravityMod.Value.ToString("0.###") +
					" weight=" + stunWeight.ToString("0.###"));
			}

			// Apply pose — same flying amount that drives the rotation, so pose and orientation stay in sync.
			stunWeight *= smoothedFlyingAmount;

			animatorPoser.ProvideInstructions(airbornePose, PoserLayerConstants.BODY, pose, 11, stunWeight);
			armsMod.SetValue(stunWeight.Invert());
		}

		private void CleanTimers()
		{
			recoveryTimer?.Dispose();
			recoveryTimer = null;

			crashTimer?.Dispose();
			crashTimer = null;
		}
	}
}
