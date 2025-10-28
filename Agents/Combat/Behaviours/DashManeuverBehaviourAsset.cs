using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "Behaviour_Maneuver_Dash", menuName = "ScriptableObjects/Combat/DashManeuverBehaviourAsset")]
	public class DashManeuverBehaviourAsset : CorePerformanceMoveBehaviourAsset, IPrerequisite
	{
		protected float DashSpeed => dashSpeed * (dashSpeedStat ?? 1f);
		protected float DashDuration => dashDistance / DashSpeed;
		protected float GlideSpeed => dashSpeed * (glideSpeedStat ?? 1f);

		[Header("Control")]
		[SerializeField] private float maxAcceleration = 20000f;
		[SerializeField] private float maxDeceleration = 2000f;
		[SerializeField] private float power = 50f;
		[Header("Dashing")]
		[SerializeField] private float dashDistance = 5f;
		[SerializeField] private float dashSpeed = 10f;
		[Header("Gliding")]
		//[SerializeField] private float glideDelay = 0.25f;
		[SerializeField] private float glideSpeed = 5f;
		[Header("SFX")]
		[SerializeField] private SFXData dashSFX;
		[SerializeField] private SFXData glideSFX;
		[SerializeField] private float glideFadeout = 0.2f;

		private CallbackService callbackService;
		private IAgentMovementHandler movementHandler;
		private AwarenessComponent awarenessComponent;
		private Pool<PooledAudioSource> audioPool;

		private EntityStat massStat;
		private EntityStat dashSpeedStat;
		private EntityStat glideSpeedStat;
		private AudioSourceWrapper glideAudio;
		private Vector3 direction;

		public bool IsMet(IDependencyManager dependencies)
		{
			return dependencies.TryGet(out AgentStatHandler statHandler) &&
				!statHandler.PointStats.E.IsRecoveringFromZero;
		}

		public void InjectDependencies(CallbackService callbackService, IAgentMovementHandler movementHandler,
			AwarenessComponent awarenessComponent, Pool<PooledAudioSource> audioPool)
		{
			this.callbackService = callbackService;
			this.movementHandler = movementHandler;
			this.awarenessComponent = awarenessComponent;
			this.audioPool = audioPool;

			massStat = Agent.Stats.GetStat(AgentStatIdentifiers.MASS);
			dashSpeedStat = Agent.Stats.GetStat(AgentStatIdentifiers.DASH_SPEED);
			glideSpeedStat = Agent.Stats.GetStat(AgentStatIdentifiers.GLIDE_SPEED);
		}

		public override void Start()
		{
			base.Start();
			callbackService.SubscribeUpdate(UpdateMode.FixedUpdate, this, OnFixedUpdate);

			InitiateDash();
		}

		public override void Stop()
		{
			base.Stop();
			callbackService.UnsubscribeUpdate(UpdateMode.FixedUpdate, this);
			glideAudio.FadeOut(glideFadeout, EasingMethod.InOutSine);
			movementHandler.AutoUpdateMovement = true;
		}

		private void InitiateDash()
		{
			SetDirection(movementHandler.InputRaw);

			// Disable default movement application from interfering.
			movementHandler.AutoUpdateMovement = false;

			// Drain stat.
			float cost = massStat * DashSpeed * Move.ChargeCost.Cost * 0.1f;
			Agent.Stats.TryApplyStatCost(Move.ChargeCost.Stat, cost, false);

			// Report impact for awareness.
			// TODO: Make continuous while gliding.
			awarenessComponent.ReportImpact(new ImpactData()
			{
				Source = Agent,
				Direction = direction,
				Location = Agent.Transform.position,
				Force = 10f // Overcome Log10
			});

			// Play dash SFX.
			dashSFX.Play(audioPool.Request(Agent.Transform.position, Agent.Transform).AudioSourceWrapper);

			// Start glide SFX.
			glideAudio = audioPool.Request(Agent.Transform.position, Agent.Transform).AudioSourceWrapper;
			glideSFX.PlayLoop(glideAudio, true);
		}

		// Applies physics.
		private void OnFixedUpdate(float delta)
		{
			if (Performer.Charge < DashDuration)
			{
				// Apply dash control.
				RigidbodyWrapper.ApplyMovement(direction * DashSpeed, maxAcceleration, maxDeceleration, power, true);
				movementHandler.UpdateRotation(delta, null, true);
			}
			else
			{
				if (RigidbodyWrapper.Speed < 0.1f)
				{
					// Exit dash.
					Exit();
					return;
				}

				// Apply glide control.
				Vector3 velocity = Quaternion.LookRotation(movementHandler.InputAxis) * movementHandler.InputSmooth.ClampMagnitude(1f) * GlideSpeed;
				RigidbodyWrapper.ApplyMovement(velocity, maxAcceleration, maxDeceleration, power, true);
				movementHandler.UpdateRotation(delta, null, true);
			}
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

			if (Performer.Charge < DashDuration)
			{
				SetDirection(movementHandler.InputSmooth);
			}

			if (State == PerformanceState.Preparing && Performer.Charge > DashDuration)
			{
				// Gliding, drain stat.
				float cost = massStat * GlideSpeed * Move.ChargeCost.Cost * delta * 0.1f;
				if (Agent.Stats.TryApplyStatCost(Move.ChargeCost.Stat, cost, false, out _, out bool drained) && drained)
				{
					// Exit dash.
					Exit();
				}
			}

			// Update glide SFX.
			glideAudio.Pitch.BaseValue = glideSFX.PitchRange.Lerp(RigidbodyWrapper.Speed / glideSpeed);
			glideAudio.Volume.BaseValue = (Performer.Charge / DashDuration).Clamp01() * glideSFX.VolumeRange.Lerp(RigidbodyWrapper.Speed / glideSpeed);
		}

		protected override IPoserInstructions Evaluate(out float weight)
		{
			Vector3 input = RigidbodyWrapper.RelativeVelocity;
			IPoserInstructions instructions = Move.PosingData.GetInstructions(Performer.Charge, input);

			weight = ((Performer.RunTime - Move.MinDuration) / Move.Release).InvertClamped().InOutSine();
			weight *= (Performer.CancelTime / Move.CancelDuration).InvertClamped();

			return instructions;
		}

		private void SetDirection(Vector3 input)
		{
			// Set initial direction.
			direction = input == Vector3.zero ?
				RigidbodyWrapper.Forward :
				Quaternion.LookRotation(movementHandler.InputAxis) * input.normalized;

			// Override smooth input to match dash direction, preventing sudden brake when stopping.
			Vector3 inputOverride = (Quaternion.LookRotation(movementHandler.InputAxis).Inverse() * direction).normalized;
			movementHandler.InputSmooth = inputOverride;
		}

		private void Exit()
		{
			movementHandler.AutoUpdateMovement = true;
			Performer.TryPerform();
		}
	}
}
