using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentStunHandlerComponent : EntityComponentBase, IStunHandler
	{
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
		[SerializeField] private float crashStickVelocity = 25f;
		[SerializeField] private float crashStickTime = 3f;
		[SerializeField] private float crashDetectionRadius = 0.3f;
		[SerializeField] private LayerMask crashDetectionMask;

		private IAgent agent;
		private RigidbodyWrapper rigidbodyWrapper;
		private AnimatorPoser animatorPoser;
		private IAgentMovementHandler movementHandler;
		private AgentArmsComponent arms;
		private IGrounderComponent grounder;

		private Pose airbornePose;
		private Pose flooredPose;
		private Pose crashPose;
		private Pose fallPose;

		private FloatOperationModifier controlMod;
		private FloatOperationModifier armsMod;
		private FloatOperationModifier gravityMod;

		private HitData stunHit;
		private TimerStruct stunTimer;

		private bool blasted;
		private TimerStruct airborneTimer;
		private TimerStruct? getUpTimer;
		private TimerStruct? crashTimer;
		private RaycastHit crashHit;

		public void InjectDependencies(IAgent agent, RigidbodyWrapper rigidbodyWrapper,
			AnimatorPoser animatorPoser, IAgentMovementHandler movementHandler,
			AgentArmsComponent arms, IGrounderComponent grounder)
		{
			this.agent = agent;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.animatorPoser = animatorPoser;
			this.movementHandler = movementHandler;
			this.arms = arms;
			this.grounder = grounder;
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
			stunTimer = new TimerStruct(minStunTime);
			controlMod.SetValue(0f);
			agent.Actor.Blocked = true;
			getUpTimer = null;
			crashTimer = null;

			if (blasted = rigidbodyWrapper.Speed > blastThreshold)
			{
				airborneTimer = new TimerStruct(minAirborneLength);
			}
		}

		public void ExitStun()
		{
			Stunned = false;

			animatorPoser.RevokeInstructions(this);

			controlMod.SetValue(1f);
			armsMod.SetValue(1f);
			gravityMod.SetValue(1f);

			agent.Actor.Blocked = false;
		}

		private void UpdateGroundedStun()
		{
			float stunAmount = Mathf.Max(stunTimer.Progress.Invert(), Mathf.InverseLerp(recoveredThreshold, recoveryThreshold, rigidbodyWrapper.Speed));

			IPoserInstructions instructions = hitBlendTree.GetInstructions(0f, -stunHit.Direction.Localize(rigidbodyWrapper.transform));
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
			if (!crashTimer.HasValue &&
				Physics.SphereCast(agent.Targetable.Center, crashDetectionRadius, -direction, out crashHit, crashDetectionRadius + direction.magnitude * Time.fixedDeltaTime, crashDetectionMask) &&
				Vector3.Angle(crashHit.normal, direction.normalized) < crashAngle)
			{
				crashTimer = new TimerStruct(crashStickTime);
				rigidbodyWrapper.Velocity = Vector3.zero;
			}

			if (crashTimer.HasValue)
			{
				gravityMod.SetValue(crashTimer.Value.Progress.InOutCubic());
				direction = Vector3.Lerp(crashHit.normal, direction, crashTimer.Value.Progress.InOutCubic());
			}

			movementHandler.ForceRotation(direction);

			PoseTransition blastedPose = new PoseTransition(airbornePose, flooredPose, groundedAmount);
			PoseTransition fallingPose = new PoseTransition(crashPose, fallPose, crashTimer.HasValue ? crashTimer.Value.Progress : 1f);
			float fallAmount = Mathf.Max(crashTimer.HasValue ? crashTimer.Value.Progress.Invert() : 0f, Mathf.InverseLerp(blastThreshold, fallThreshold, rigidbodyWrapper.Speed));
			float blend = grounder.Grounded && !crashTimer.HasValue ? 0f : fallAmount;
			PoserInstructions pose = new PoserInstructions(blastedPose, fallingPose, blend);

			float stunWeight = 1f;
			if (getUpTimer.HasValue)
			{
				stunWeight *= getUpTimer.Value.Progress.Invert();
			}
			else if (rigidbodyWrapper.Speed < recoveryThreshold)
			{
				getUpTimer = new TimerStruct(getUpTime);
			}

			SpaxDebug.Log("Blasted", $"crash={(crashTimer.HasValue ? crashTimer.Value.Time : 0f)}, ground={groundedAmount}, fall={fallAmount}, blend={blend}, weight={stunWeight}");

			animatorPoser.ProvideInstructions(this, PoserLayerConstants.BODY, pose, 10, stunWeight);
			armsMod.SetValue(stunWeight.Invert());

			if (getUpTimer.HasValue && getUpTimer.Value.Expired)
			{
				ExitStun();
			}
		}
	}
}
