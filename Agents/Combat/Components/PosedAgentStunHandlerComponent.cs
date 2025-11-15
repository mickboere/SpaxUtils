using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[DefaultExecutionOrder(100)]
	public class PosedAgentStunHandlerComponent : AgentStunHandlerComponent
	{
		protected override bool DefaultExitBehavior => false;
		protected bool Debug => debug && Entity.RuntimeData.GetValue<bool>(EntityDataIdentifiers.DEBUG, true);

		[Header("Grounded")]
		[SerializeField] private PoseBlendMap hitBlendTree;
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

			if (Stunned)
			{
				flying = flying || rigidbodyWrapper.Velocity.FlattenY().magnitude > horizontalFlyThreshold || rigidbodyWrapper.Velocity.y > verticalFlyThreshold;
				if (flying && airborneTimer == null)
				{
					airborneTimer = new TimerClass(minAirborneLength, () => EntityTimeScale, callbackService, UpdateMode.FixedUpdate);
				}

				if (flying)
				{
					UpdateFlyingStun();
				}
				else
				{
					UpdateGroundedStun();
				}
			}
		}

		public override void EnterStun(HitData stunHit, float duration = -1f)
		{
			base.EnterStun(stunHit, duration);

			flying = false;
			CleanTimers();

			if (Debug)
			{
				SpaxDebug.Log($"EnterStun ({(flying ? "flying" : "grounded")}) [{duration}s]", $"V={rigidbodyWrapper.Velocity}\n{stunHit}");
			}
		}

		public override void ExitStun()
		{
			base.ExitStun();

			animatorPoser.RevokeInstructions(this);
			armsMod.SetValue(1f);
			gravityMod.SetValue(1f);
			CleanTimers();

			if (Debug)
			{
				SpaxDebug.Log($"ExitStun", stunHit.ToString());
			}
		}

		private void UpdateGroundedStun()
		{
			IPoserInstructions instructions = hitBlendTree.GetInstructions(0f, -stunHit.Direction.LocalizeDirection(rigidbodyWrapper.transform));
			animatorPoser.ProvideInstructions(this, PoserLayerConstants.BODY, instructions, 10, stunAmount);
			armsMod.SetValue(stunAmount.Invert());

			if (Debug)
			{
				SpaxDebug.Log("Stun: Grounded",
					$"velocity={rigidbodyWrapper.Velocity}" +
					$"crashTimer={(crashTimer != null ? crashTimer.Time : "NULL")}," +
					$"getUpTimer={(getUpTimer != null ? getUpTimer.Time : "NULL")},\n" +
					$"stunAmount={stunAmount}");
			}

			if (stunTimer.Expired && rigidbodyWrapper.Speed < recoveredThreshold)
			{
				ExitStun();
			}
		}

		private void UpdateFlyingStun()
		{
			float groundedAmount = grounder.GroundedAmount * airborneTimer.Progress.Clamp01().InOutQuad();
			Vector3 direction = -rigidbodyWrapper.Velocity.MultY(groundedAmount.Invert()); // Apply terrain normal.

			if (Debug)
			{
				UnityEngine.Debug.DrawLine(Agent.Targetable.Center, Agent.Targetable.Center + -direction.normalized * (crashDetectionRadius + direction.magnitude * Time.fixedDeltaTime), Color.red);
			}

			if (crashTimer == null &&
				Physics.SphereCast(Agent.Targetable.Center, crashDetectionRadius, -direction, out crashHit, crashDetectionRadius + direction.magnitude * Time.fixedDeltaTime, crashDetectionMask) &&
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
				gravityMod.SetValue(crashTimer.Progress.Clamp01().InOutCubic());
				direction = Vector3.Lerp(crashHit.normal, direction, crashTimer.Progress.Clamp01().InOutCubic());
			}

			movementHandler.ForceRotation(direction);

			PoseTransition blastedPose = new PoseTransition(airbornePose, flooredPose, groundedAmount);
			PoseTransition fallingPose = new PoseTransition(crashPose, fallPose, crashTimer != null ? crashTimer.Progress.Clamp01() : 1f);
			float fallAmount = Mathf.Max(crashTimer != null ? crashTimer.Progress.InvertClamped() : 0f, Mathf.InverseLerp(horizontalFlyThreshold, fallThreshold, rigidbodyWrapper.Speed));
			float blend = grounder.Grounded && crashTimer == null ? 0f : fallAmount;
			PoserInstructions pose = new PoserInstructions(blastedPose, fallingPose, blend);

			float stunWeight = 1f;
			if (getUpTimer != null)
			{
				stunWeight *= getUpTimer.Progress.InvertClamped();
			}
			else if (grounder.Grounded && rigidbodyWrapper.Speed < recoveryThreshold)
			{
				getUpTimer = new TimerClass(getUpTime, () => EntityTimeScale, callbackService, UpdateMode.FixedUpdate);
			}

			if (Debug)
			{
				SpaxDebug.Log("Stun: Flying!",
					$"velocity={rigidbodyWrapper.Velocity}" +
					$"crashTimer={(crashTimer != null ? crashTimer.Time : "NULL")}," +
					$"getUpTimer={(getUpTimer != null ? getUpTimer.Time : "NULL")},\n" +
					$"ground={groundedAmount}," +
					$"fall={fallAmount}," +
					$"blend={blend}," +
					$"gravityMod={gravityMod.Value}," +
					$"weight={stunWeight}");
			}

			animatorPoser.ProvideInstructions(this, PoserLayerConstants.BODY, pose, 100, stunWeight);
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
