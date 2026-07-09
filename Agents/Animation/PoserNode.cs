using SpaxUtils;
using SpaxUtils.StateMachines;
using System.Linq;
using UnityEngine;

namespace SpiritAxis
{
	public class PoserNode : StateComponentNodeBase
	{
		protected Poser Poser => agentPoser.GetMainPoser(PoserLayerConstants.BODY);

		[SerializeField, Tooltip("Default moveset. Overridden by an injected AgentMoveset if present.")]
		private AgentAnimationSet moveset;

		[Header("Skid (direction-change sliding)")]
		[SerializeField, Range(0f, 20f), Tooltip("How quickly the skid animation appears on sharp direction changes.")]
		private float skidRampUp = 15f;
		[SerializeField, Range(0f, 10f), Tooltip("How slowly the skid animation fades after the direction change.")]
		private float skidDecay = 2f;

		/// <summary>
		/// Landing pose priority. Above base moveset (0), above animated actions (5), below combat (10).
		/// </summary>
		private const int LANDING_POSE_PRIORITY = 8;

		/// <summary>
		/// Raw-input magnitude (as a multiple of the movement deadzone) above which a HALTED agent is still treated
		/// as mid-locomotion — chiefly a direction reversal, whose velocity/InputSmooth momentarily pass through
		/// zero while intent stays high — so the idle pose stays suppressed instead of flashing at the origin.
		/// </summary>
		private const float REVERSAL_INTENT_FACTOR = 2f;

		private IAgent agent;
		private RigidbodyWrapper rigidbodyWrapper;
		private AnimatorPoser agentPoser;
		private IAgentMovementHandler movementHandler;
		private SurveyorComponent surveyorComponent;
		private CallbackService callbackService;
		private GrounderComponent grounder;

		private EntityStat timescale;
		private EntityStat moveSpeedStat;
		private EntityStat sprintSpeedStat;
		private TimerClass targetingTimer;
		private Vector3 blendPosition;
		private float slideWeight;
		private float flyWeight;
		private float targetingWeight;
		private float idleWeight = 1f;
		private float idleTime;

		// Skid tracking (direction-change sliding, separate from slope sliding).
		private float skidAmount;

		// Runtime blend maps with idle overrides. Null if no override needed.
		private PoseBlendMap passiveGroundedTree;
		private PoseBlendMap combatGroundedTree;

		// Landing state.
		private bool isLanding;
		private float landingSeverity;
		private float landingTimer;
		private float landingWeight;
		private FloatOperationModifier landingControlMod;

		/// <summary>
		/// Returns the appropriate grounded blend tree based on the current brain state.
		/// Combat (untargeted) uses combat idle override, passive uses passive idle override.
		/// Falls back to the base grounded blend tree if no override exists.
		/// </summary>
		private PoseBlendMap ActiveGroundedTree
		{
			get
			{
				if (agent.Brain.IsStateActive(AgentStateIdentifiers.COMBAT))
				{
					return combatGroundedTree != null ? combatGroundedTree : moveset.GroundedBlendTree;
				}
				return passiveGroundedTree != null ? passiveGroundedTree : moveset.GroundedBlendTree;
			}
		}

		public void InjectDependencies(IAgent agent, RigidbodyWrapper rigidbodyWrapper, AnimatorPoser agentPoser,
			IAgentMovementHandler movementHandler, GrounderComponent grounder,
			SurveyorComponent surveyorComponent, CallbackService callbackService,
			[Optional] AgentAnimationSet injectedMoveset)
		{
			this.agent = agent;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.agentPoser = agentPoser;
			this.movementHandler = movementHandler;
			this.surveyorComponent = surveyorComponent;
			this.callbackService = callbackService;
			this.grounder = grounder;

			// Injected moveset overrides the serialized default.
			if (injectedMoveset != null)
			{
				moveset = injectedMoveset;
			}

			timescale = agent.Stats.GetStat(EntityStatIdentifiers.TIMESCALE);
			moveSpeedStat = agent.Stats.GetStat(AgentStatIdentifiers.MOVEMENT_SPEED, true, 1f);
			sprintSpeedStat = agent.Stats.GetStat(AgentStatIdentifiers.SPRINT_SPEED, true, 1f);

			// Build runtime blend maps with idle overrides.
			BuildIdleOverrides();
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate, 100);
			agent.Targeter.TargetChangedEvent += OnTargetChangedEvent;
			grounder.LandedEvent += OnLanded;

			targetingTimer = new TimerClass(moveset.TargetSwitchDuration, () => timescale, false);
			targetingTimer.Progress = 1f;

			// Assume stationary on entry so the idle pose is present until movement intent says otherwise.
			idleWeight = 1f;

			// Landing control modifier (always registered, value 1.0 = no effect when not landing).
			landingControlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			rigidbodyWrapper.Control.AddModifier(this, landingControlMod);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			callbackService.UnsubscribeUpdates(this);
			agent.Targeter.TargetChangedEvent -= OnTargetChangedEvent;
			grounder.LandedEvent -= OnLanded;

			agentPoser.RevokeInstructions(this);
			agentPoser.RevokeInstructions(moveset.TargetingBlendTree);
			agentPoser.RevokeInstructions(moveset.SlidingBlendTree);
			agentPoser.RevokeInstructions(moveset.FlyingBlendTree);

			// Walking instructions should be kept to prevent null-pose issue.
			// To demonstrate: agentPoser.RevokeInstructions(moveset.GroundedBlendTree);

			rigidbodyWrapper.Control.RemoveModifier(this);
			landingControlMod = null;

			targetingTimer.Dispose();

			isLanding = false;
		}

		protected void OnDestroy()
		{
			// Clean up runtime blend map instances.
			if (passiveGroundedTree != null)
			{
				Destroy(passiveGroundedTree);
				passiveGroundedTree = null;
			}
			if (combatGroundedTree != null)
			{
				Destroy(combatGroundedTree);
				combatGroundedTree = null;
			}
		}

		private void OnUpdate(float delta)
		{
			float scaledDelta = delta * (timescale != null ? (float)timescale : 1f);

			Vector3 velocity = rigidbodyWrapper.RelativeVelocity * surveyorComponent.Influence.OutQuad();
			float max = movementHandler.FullSpeed * sprintSpeedStat.ModdedBaseValue * moveSpeedStat.ModdedBaseValue;
			blendPosition = blendPosition.FILerp(velocity / max,
				moveset.PositionBlendSpeed * delta);
			slideWeight = grounder.SlidingAmount;
			// Asymmetric: fast ramp into flying (becoming airborne), slow decay on landing
			// so the transition from flying pose to grounded pose is gradual.
			float flyTarget = 1f - grounder.GroundedAmount;
			float flySpeed = flyTarget > flyWeight
				? moveset.PoseTransitionSpeed
				: moveset.PoseTransitionSpeed * 0.5f;
			flyWeight = flyWeight.FILerp(flyTarget, flySpeed * delta);
			targetingTimer.Update(delta);

			// Skid from direction changes: inverted grip gated by speed.
			// Grip is now directional alignment, so low grip = opposing velocity = skid.
			float speed = rigidbodyWrapper.Velocity.FlattenY().magnitude;
			float speedFactor = Mathf.Clamp01(speed / movementHandler.FullSpeed);
			float skidTarget = rigidbodyWrapper.Grip.InvertClamped() * speedFactor;

			// Fast ramp up, slow decay for visual linger.
			if (skidTarget > skidAmount)
			{
				skidAmount = Mathf.Lerp(skidAmount, skidTarget, skidRampUp * delta);
			}
			else
			{
				skidAmount = Mathf.MoveTowards(skidAmount, 0f, skidDecay * delta);
			}

			// Advance idle time for stationary idle animation.
			idleTime += scaledDelta;

			// Update landing state.
			UpdateLanding(scaledDelta);

			// Idle ONLY when the movement is actually halted AND it isn't a mid-reversal. Two signals:
			//  • halted    = InputSmooth below the deadzone — the SAME test GroundedMovementHandler uses to zero its
			//    desired velocity (so it accounts for the input-ramp curve + smoothing). Keying on raw InputRaw
			//    mismatched at the deadzone EDGE: the ramp pulls the effective input below the deadzone (movement
			//    stops) while raw input still read above it → walk-in-place at a standstill.
			//  • reversing = raw input still holds a clear direction (>= a small multiple of the deadzone). During a
			//    reversal InputSmooth dips through zero while intent stays high, so this keeps the idle suppressed
			//    and lets the origin crossing cross-blend locomotion poses instead of flashing the upright idle.
			bool halted = movementHandler.InputSmooth.magnitude < movementHandler.MinimumInput;
			bool reversing = movementHandler.InputRaw.magnitude >= movementHandler.MinimumInput * REVERSAL_INTENT_FACTOR;
			float idleTarget = halted && !reversing ? 1f : 0f;
			idleWeight = idleWeight.FILerp(idleTarget, moveset.PoseTransitionSpeed * delta);

			UpdateWalkingPose(delta);
		}

		private void OnTargetChangedEvent(ITargetable target)
		{
			targetingTimer.Reset();
		}

		private void OnLanded(float impactSpeed, RaycastHit hit)
		{
			if (moveset.LandingPose == null)
			{
				return;
			}

			if (impactSpeed < moveset.LandingMinImpact)
			{
				return;
			}

			landingSeverity = Mathf.InverseLerp(moveset.LandingMinImpact, moveset.LandingMaxImpact, impactSpeed).OutQuad();
			landingTimer = 0f;
			isLanding = true;
		}

		private void UpdateLanding(float delta)
		{
			if (!isLanding)
			{
				landingWeight = 0f;
				landingControlMod?.SetValue(1f);
				return;
			}

			landingTimer += delta;

			float holdDuration = moveset.LandingHoldDuration * landingSeverity;
			float fadeOut = moveset.LandingFadeOut;
			float totalDuration = holdDuration + fadeOut;

			if (landingTimer >= totalDuration)
			{
				isLanding = false;
				landingWeight = 0f;
				agentPoser.RevokeInstructions(this);
			}
			else if (landingTimer < holdDuration)
			{
				// Holding at severity weight.
				landingWeight = landingSeverity;
			}
			else
			{
				// Fading out.
				float fadeProgress = (landingTimer - holdDuration) / fadeOut;
				landingWeight = landingSeverity * (1f - Mathf.Clamp01(fadeProgress));
			}

			// Scale by GroundedAmount so landing pose blends in as the agent settles
			// instead of snapping when the ground check first enters range.
			landingWeight *= grounder.GroundedAmount;

			// Reduce movement control proportional to landing weight.
			float control = 1f - (landingWeight * moveset.LandingControlReduction);
			landingControlMod?.SetValue(control);
		}

		private void UpdateWalkingPose(float delta)
		{
			IPoserInstructions walking = GetWalkPose(ActiveGroundedTree, blendPosition, idleWeight);
			Poser.Pose(walking); // A main pose is required.

			IPoserInstructions targeting = GetWalkPose(moveset.TargetingBlendTree, blendPosition, idleWeight);
			// Targeting (locked-on strafe) overlay weight must EASE, not snap. LockRotation flips the instant
			// sprint is pressed (raw input), so the old hard ternary popped this 1->0 in one frame, yanking the
			// hunched strafe pose off and exposing the grounded tree mid-reversal. Lerp toward the desired weight.
			float targetingTarget = movementHandler.LockRotation &&
				agent.Brain.IsStateActive(AgentStateIdentifiers.COMBAT) ?
					targetingTimer.Progress.Clamp01() : 0f;
			targetingWeight = targetingWeight.FILerp(targetingTarget, moveset.PoseTransitionSpeed * delta);
			agentPoser.ProvideInstructions(moveset.TargetingBlendTree, PoserLayerConstants.BODY, targeting, 1, targetingWeight);

			IPoserInstructions sliding = moveset.SlidingBlendTree.GetInstructions(0f,
				grounder.Sliding ? blendPosition : -rigidbodyWrapper.RelativeVelocity.normalized);
			agentPoser.ProvideInstructions(moveset.SlidingBlendTree, PoserLayerConstants.BODY, sliding, 2,
				Mathf.Max(slideWeight, skidAmount));

			IPoserInstructions flying = moveset.FlyingBlendTree.GetInstructions(0f, blendPosition);
			agentPoser.ProvideInstructions(moveset.FlyingBlendTree, PoserLayerConstants.BODY, flying, 3, flyWeight);

			// Landing pose overlay. Single pose, weight = severity.
			float landingOverlayWeight = landingWeight;

			if (!isLanding && grounder.IsLanding)
			{
				float downwardSpeed = Mathf.Max(0f, -rigidbodyWrapper.Velocity.y);
				float severity = Mathf.InverseLerp(moveset.LandingMinImpact, moveset.LandingMaxImpact, downwardSpeed).OutQuad();

				landingOverlayWeight = severity * grounder.GroundedAmount;
			}

			if (landingOverlayWeight > 0f && moveset.LandingPose != null)
			{
				IPoserInstructions landingInstructions = moveset.LandingPose.GetInstructions(0f);
				agentPoser.ProvideInstructions(this, PoserLayerConstants.BODY, landingInstructions, LANDING_POSE_PRIORITY, landingOverlayWeight);
			}
			else
			{
				agentPoser.RevokeInstructions(this);
			}
		}

		private IPoserInstructions GetWalkPose(PoseBlendMap blendTree, Vector3 position, float idleWeight)
		{
			// Find the idle (center) sequence so it can keep its own wall clock while locomotion
			// sequences ride the surveyor gait phase. Switching the WHOLE blend's clock by blendPosition
			// magnitude phase-jumped the pose every time velocity crossed ~zero (every stop / reversal),
			// because the two clocks are uncorrelated. Per-sequence clocks remove the swap, and thus the pop.
			PoseSequence idleSequence = null;
			foreach (PoseBlendMapEntry entry in blendTree.BlendMap)
			{
				if (entry.Position == Vector3.zero)
				{
					idleSequence = entry.Sequence;
					break;
				}
			}

			return blendTree.GetInstructions(position, (IPoseSequence sequence) =>
			{
				if (ReferenceEquals(sequence, idleSequence))
				{
					// Idle/center pose: free-running wall clock so it breathes while stationary.
					return sequence.TotalDuration > 0f
						? Mathf.Repeat(idleTime, sequence.TotalDuration)
						: 0f;
				}

				// Locomotion pose: surveyor gait phase so strides stay planted.
				sequence.GlobalData.TryGetFloat(AnimationFloatConstants.CYCLE_OFFSET, 0f, out float cycleOffset);
				return surveyorComponent.GetProgress(cycleOffset, false) * sequence.TotalDuration;
			}, idleWeight);
		}

		/// <summary>
		/// Builds runtime copies of the grounded blend tree with idle overrides swapped in.
		/// Only creates copies when an override exists; otherwise the original blend tree is used.
		/// </summary>
		private void BuildIdleOverrides()
		{
			passiveGroundedTree = moveset.GroundedBlendTree.CreateWithIdleOverride(moveset.PassiveIdle);
			combatGroundedTree = moveset.GroundedBlendTree.CreateWithIdleOverride(moveset.CombatIdle);
		}
	}
}
