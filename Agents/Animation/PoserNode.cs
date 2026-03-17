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
		private AgentMoveset moveset;

		[Header("Skid (direction-change sliding)")]
		[SerializeField, Range(0f, 20f), Tooltip("How quickly the skid animation appears on sharp direction changes.")]
		private float skidRampUp = 15f;
		[SerializeField, Range(0f, 10f), Tooltip("How slowly the skid animation fades after the direction change.")]
		private float skidDecay = 2f;

		/// <summary>
		/// Landing pose priority. Above base moveset (0), above animated actions (5), below combat (10).
		/// </summary>
		private const int LANDING_POSE_PRIORITY = 8;

		private IAgent agent;
		private RigidbodyWrapper rigidbodyWrapper;
		private AnimatorPoser agentPoser;
		private IAgentMovementHandler movementHandler;
		private SurveyorComponent surveyorComponent;
		private CallbackService callbackService;
		private GrounderComponent grounder;

		private EntityStat timescale;
		private EntityStat moveSpeedStat;
		private TimerClass targetingTimer;
		private Vector3 blendPosition;
		private float slideWeight;
		private float flyWeight;
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
			[Optional] AgentMoveset injectedMoveset)
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
			float max = movementHandler.FullSpeed * moveSpeedStat.ModdedBaseValue;
			blendPosition = blendPosition.FILerp(velocity / max,
				moveset.PositionBlendSpeed * delta);
			slideWeight = grounder.SlidingAmount;
			flyWeight = flyWeight.FILerp(grounder.Grounded ? 0f : 1f, moveset.PoseTransitionSpeed * delta);
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

			UpdateWalkingPose();
		}

		private void OnTargetChangedEvent(ITargetable target)
		{
			targetingTimer.Reset();
		}

		private void OnLanded(float impactSpeed)
		{
			if (moveset.LandingPose == null)
			{
				return;
			}

			if (impactSpeed < moveset.LandingMinImpact)
			{
				return;
			}

			landingSeverity = Mathf.InverseLerp(moveset.LandingMinImpact, moveset.LandingMaxImpact, impactSpeed);
			landingSeverity = Mathf.Clamp01(landingSeverity);
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

			// Reduce movement control proportional to landing weight.
			float control = 1f - (landingWeight * moveset.LandingControlReduction);
			landingControlMod?.SetValue(control);
		}

		private void UpdateWalkingPose()
		{
			IPoserInstructions walking = GetWalkPose(ActiveGroundedTree, blendPosition);
			Poser.Pose(walking); // A main pose is required.

			IPoserInstructions targeting = GetWalkPose(moveset.TargetingBlendTree, blendPosition);
			agentPoser.ProvideInstructions(moveset.TargetingBlendTree, PoserLayerConstants.BODY, targeting, 1,
				movementHandler.LockRotation &&
				agent.Brain.IsStateActive(AgentStateIdentifiers.COMBAT) ?
					targetingTimer.Progress.Clamp01() :
					targetingTimer.Progress.InvertClamped()); // HACK: This is ugly but fuck you, I'm making a game not a system.

			IPoserInstructions sliding = moveset.SlidingBlendTree.GetInstructions(0f,
				grounder.Sliding ? blendPosition : -rigidbodyWrapper.RelativeVelocity.normalized);
			agentPoser.ProvideInstructions(moveset.SlidingBlendTree, PoserLayerConstants.BODY, sliding, 2,
				Mathf.Max(slideWeight, skidAmount));

			IPoserInstructions flying = moveset.FlyingBlendTree.GetInstructions(0f, blendPosition);
			agentPoser.ProvideInstructions(moveset.FlyingBlendTree, PoserLayerConstants.BODY, flying, 3, flyWeight);

			// Landing pose overlay. Single pose, weight = severity.
			if (isLanding && moveset.LandingPose != null)
			{
				IPoserInstructions landingInstructions = moveset.LandingPose.GetInstructions(0f);
				agentPoser.ProvideInstructions(this, PoserLayerConstants.BODY, landingInstructions, LANDING_POSE_PRIORITY, landingWeight);
			}
		}

		private IPoserInstructions GetWalkPose(PoseBlendMap blendTree, Vector3 position)
		{
			return blendTree.GetInstructions(position, (IPoseSequence sequence) =>
			{
				sequence.GlobalData.TryGetFloat(AnimationFloatConstants.CYCLE_OFFSET, 0f, out float cycleOffset);
				if (position.sqrMagnitude > 0.001f)
				{
					// Moving: use walk cycle time from surveyor.
					return surveyorComponent.GetProgress(cycleOffset, false) * sequence.TotalDuration;
				}
				else
				{
					// Stationary: use real time for idle sequence.
					return sequence.TotalDuration > 0f
						? Mathf.Repeat(idleTime, sequence.TotalDuration)
						: 0f;
				}
			});
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
