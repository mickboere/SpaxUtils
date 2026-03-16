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

		// Landing state.
		private bool isLanding;
		private float landingSeverity;
		private float landingTimer;
		private float landingWeight;
		private FloatOperationModifier landingControlMod;

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
			IPoserInstructions walking = GetWalkPose(moveset.GroundedBlendTree, blendPosition);
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
				Mathf.Max(slideWeight, rigidbodyWrapper.Grip.InvertClamped().InOutQuint()));

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
				if (surveyorComponent.Influence > 0f)
				{
					// Surveyor active: use walk cycle time.
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
	}
}
