using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[DefaultExecutionOrder(100)]
	public class PosedAgentStunHandlerComponent : AgentStunHandlerComponent
	{
		#region Tooltips
		private const string TT_RECOVERY_THRESH = "Upper velocity threshold below which Agent begins to recover.";
		private const string TT_RECOVERED_THRESH = "Lower velocity threshold below which control is fully returned to Agent.";
		#endregion Tooltips

		protected override bool DefaultExitBehavior => false;
		protected bool Debug => debug && Entity.RuntimeData.GetValue<bool>(EntityDataIdentifiers.DEBUG, true);

		[Header("Grounded")]
		[SerializeField] private PoseBlendMap hitBlendTree;
		[SerializeField, Tooltip(TT_RECOVERY_THRESH)] protected float recoveryThreshold = 3f;
		[SerializeField, Tooltip(TT_RECOVERED_THRESH)] protected float recoveredThreshold = 2f;

		[Header("Flying")]
		[SerializeField] private float horizontalFlyThreshold = 15f;
		[SerializeField] private float verticalFlyThreshold = 1f;
		[SerializeField] private AnimationClip airbornePoseClip;
		[SerializeField] private float minAirborneLength = 0.7f;
		[SerializeField] private AnimationClip flooredPoseClip;
		[SerializeField] private float getUpTime = 0.5f;
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

		private bool flying;
		private float smoothedGroundedAmount;

		private TimerClass airborneTimer;
		private TimerClass getUpTimer;
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

			// Smooth grounded amount so pose logic does not snap when rotation changes affect the ground checks.
			float targetGroundedAmount = grounder != null ? grounder.GroundedAmount : 0f;
			if (groundedAmountSmoothing <= 0f)
			{
				smoothedGroundedAmount = targetGroundedAmount;
			}
			else
			{
				smoothedGroundedAmount = Mathf.MoveTowards(
					smoothedGroundedAmount,
					targetGroundedAmount,
					groundedAmountSmoothing * Time.fixedDeltaTime);
			}

			// Decide if we should enter/keep flying mode.
			bool shouldFly =
				(grounder != null && !grounder.Grounded) ||
				rigidbodyWrapper.Velocity.FlattenY().magnitude > horizontalFlyThreshold ||
				rigidbodyWrapper.Velocity.y > verticalFlyThreshold;

			flying = flying || shouldFly;

			// Ensure airborne timer exists before any flying logic uses it.
			if (flying && airborneTimer == null)
			{
				airborneTimer = new TimerClass(minAirborneLength, () => EntityTimeScale, callbackService, UpdateMode.FixedUpdate);
			}

			UpdateGroundedStun();

			// IMPORTANT:
			// UpdateGroundedStun can call ExitStun(), which disposes timers.
			// Do not run flying logic after that in the same frame.
			if (!Stunned)
			{
				return;
			}

			if (flying)
			{
				UpdateFlyingStun();
			}
		}

		public override void EnterStun(HitData stunHit, float duration = -1f)
		{
			base.EnterStun(stunHit, duration);

			flying = false;
			CleanTimers();

			smoothedGroundedAmount = grounder != null ? grounder.GroundedAmount : 0f;

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
			float stunAmount =
				Mathf.Max(
					stunTimer.Progress.InvertClamped().InOutExpo(),
					Mathf.InverseLerp(recoveredThreshold, recoveryThreshold, rigidbodyWrapper.Speed).OutQuad());

			IPoserInstructions instructions =
				hitBlendTree.GetInstructions(0f, -stunHit.Direction.LocalizeDirection(rigidbodyWrapper.transform));

			animatorPoser.ProvideInstructions(hitBlendTree, PoserLayerConstants.BODY, instructions, 10, stunAmount);
			armsMod.SetValue(stunAmount.Invert());

			if (Debug)
			{
				SpaxDebug.Log("Stun: Grounded",
					"velocity=" + rigidbodyWrapper.Velocity +
					" crashTimer=" + (crashTimer != null ? crashTimer.Time.ToString("0.###") : "NULL") +
					" getUpTimer=" + (getUpTimer != null ? getUpTimer.Time.ToString("0.###") : "NULL") +
					"\nstunAmount=" + stunAmount.ToString("0.###"));
			}

			if (stunTimer.Expired && rigidbodyWrapper.Speed < recoveredThreshold)
			{
				ExitStun();
			}
		}

		private void UpdateFlyingStun()
		{
			// Airborne timer is used to prevent "horizontal launches" from staying fully grounded visually.
			// Guard against it being null (it can be nulled by ExitStun in the same FixedUpdate if not careful).
			float airProg = airborneTimer != null ? airborneTimer.Progress.Clamp01() : 1f;

			// Use smoothed grounded amount to avoid pose snaps when rotation affects grounding checks.
			float groundedAmount = smoothedGroundedAmount * airProg.InOutQuad();

			// Apply terrain normal / grounding influence to the direction.
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

			// Force rotation towards movement direction.
			movementHandler.ForceRotation(direction);

			// Pose construction.
			PoseTransition blastedPose = new PoseTransition(airbornePose, flooredPose, groundedAmount);
			PoseTransition fallingPose = new PoseTransition(crashPose, fallPose, crashTimer != null ? crashTimer.Progress.Clamp01() : 1f);

			float fallAmount =
				Mathf.Max(
					crashTimer != null ? crashTimer.Progress.InvertClamped() : 0f,
					Mathf.InverseLerp(horizontalFlyThreshold, fallThreshold, rigidbodyWrapper.Speed));

			float blend = (grounder != null && grounder.Grounded && crashTimer == null) ? 0f : fallAmount;
			PoserInstructions pose = new PoserInstructions(blastedPose, fallingPose, blend);

			// Get-up timer.
			float stunWeight = 1f;
			if (getUpTimer != null)
			{
				stunWeight *= getUpTimer.Progress.InvertClamped();
			}
			else if (grounder != null && grounder.Grounded && rigidbodyWrapper.Speed < recoveryThreshold)
			{
				getUpTimer = new TimerClass(getUpTime, () => EntityTimeScale, callbackService, UpdateMode.FixedUpdate);
			}

			if (Debug)
			{
				SpaxDebug.Log("Stun: Flying",
					"velocity=" + rigidbodyWrapper.Velocity +
					" crashTimer=" + (crashTimer != null ? crashTimer.Time.ToString("0.###") : "NULL") +
					" getUpTimer=" + (getUpTimer != null ? getUpTimer.Time.ToString("0.###") : "NULL") +
					"\nrawGround=" + (grounder != null ? grounder.GroundedAmount.ToString("0.###") : "NULL") +
					" smGround=" + smoothedGroundedAmount.ToString("0.###") +
					" grounded=" + groundedAmount.ToString("0.###") +
					" fall=" + fallAmount.ToString("0.###") +
					" blend=" + blend.ToString("0.###") +
					" gravMod=" + gravityMod.Value.ToString("0.###") +
					" weight=" + stunWeight.ToString("0.###"));
			}

			// Apply pose.
			float groundedInvert = (grounder != null ? grounder.GroundedAmount.Invert() : 1f);
			stunWeight *= groundedInvert;

			animatorPoser.ProvideInstructions(airbornePose, PoserLayerConstants.BODY, pose, 11, stunWeight);
			armsMod.SetValue(stunWeight.Invert());

			if (getUpTimer != null && getUpTimer.Expired)
			{
				ExitStun();
			}
		}

		private void CleanTimers()
		{
			getUpTimer?.Dispose();
			getUpTimer = null;

			crashTimer?.Dispose();
			crashTimer = null;

			airborneTimer?.Dispose();
			airborneTimer = null;
		}
	}
}
