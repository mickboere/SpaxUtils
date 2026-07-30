using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(DashManeuverBehaviourAsset), menuName = "Performance/Behaviour/" + nameof(DashManeuverBehaviourAsset))]
	public class DashManeuverBehaviourAsset : CorePerformanceMoveBehaviourAsset
	{
		/// <summary>
		/// Encumberment multiplier. LoadPenalty is the shared authority on "how overloaded am I"; the exponent is how
		/// much dashing in particular cares.
		/// </summary>
		protected float LoadMod => Mathf.Max(0.01f, Mathf.Pow(loadPenaltyStat ?? 1f, loadSensitivity));

		/// <summary>
		/// Floored at normal run speed: an overloaded dash degrades into a lunging stride, never a crawl slower than the
		/// agent's own walk. Encumberment keeps biting through cost, which has no floor.
		/// </summary>
		protected float DashSpeed => Mathf.Max(movementHandler.FullSpeed, dashSpeed * (dashSpeedStat ?? 1f) * LoadMod);

		/// <summary>
		/// Real seconds the burst lasts. Distance is fixed, so a faster dash is a SHORTER one — Agility gets you out of
		/// the way quicker, never further.
		/// </summary>
		protected float DashDuration => dashDistance / DashSpeed;

		protected bool Bursting => dashTime < DashDuration;

		protected float GlideSpeed => glideSpeed * (glideSpeedStat ?? 1f) * LoadMod;

		/// <summary>
		/// Surface purchase, same authority the movement handler uses: terrain too steep to run up is too steep to dash
		/// up, downhill gets a boost. Zero while airborne, which zeroes every force term — the dash coasts ballistically.
		/// </summary>
		protected float Traction => grounder.Mobility;

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
		[SerializeField] private Vector3 shakeMagnitude = Vector3.one;
		[Header("Load")]
		[SerializeField, Tooltip("Exponent applied to the LoadPenalty stat. 1 = as encumbered as general movement, higher = dashing suffers more. Never locks the dash out, only degrades it.")]
		private float loadSensitivity = 2f;
		[Header("SFX")]
		[SerializeField] private SFXData dashSFX;
		[SerializeField] private SFXData glideSFX;
		[SerializeField] private float glideFadeout = 0.2f;

		private AgentStatHandler statHandler;
		private CallbackService callbackService;
		private IAgentMovementHandler movementHandler;
		private AgentImpactHandler senseComponent;
		private Pool<PooledAudioSource> audioPool;
		private AgentTrailEffect agentTrailEffect;
		private GrounderComponent grounder;

		private PointsStat pointStat;
		private EntityStat massStat;
		private EntityStat dashSpeedStat;
		private EntityStat glideSpeedStat;
		private EntityStat loadPenaltyStat;
		private EntityStat timeScaleStat;
		private AudioSourceWrapper glideAudio;
		private Vector3 direction;
		private ContinuousShakeSource shakeSource;

		// The burst runs on its own real-time clock. Performer.ChargeTime is a stat-PACED clock (it advances at
		// Move.ChargeSpeedMultiplierStat), so timing physics off it made a second stat silently scale displacement.
		private float dashTime;
		private bool exited;

		public override bool IsMet(IDependencyManager dependencies)
		{
			if (!base.IsMet(dependencies))
			{
				return false;
			}

			return dependencies.TryGet(out AgentStatHandler statHandler) &&
				!statHandler.PointStats.E.IsRecoveringFromZero;
		}

		public void InjectDependencies(AgentStatHandler statHandler, CallbackService callbackService, IAgentMovementHandler movementHandler,
			AgentImpactHandler senseComponent, Pool<PooledAudioSource> audioPool, AgentTrailEffect agentTrailEffect,
			GrounderComponent grounder)
		{
			this.statHandler = statHandler;
			this.callbackService = callbackService;
			this.movementHandler = movementHandler;
			this.senseComponent = senseComponent;
			this.audioPool = audioPool;
			this.agentTrailEffect = agentTrailEffect;
			this.grounder = grounder;

			statHandler.TryGetPointStat(Move.ChargeCost.Stat, out pointStat);
			massStat = Agent.Stats.GetStat(AgentStatIdentifiers.MASS);
			dashSpeedStat = Agent.Stats.GetStat(AgentStatIdentifiers.DASH_SPEED);
			glideSpeedStat = Agent.Stats.GetStat(AgentStatIdentifiers.GLIDE_SPEED);
			loadPenaltyStat = Agent.Stats.GetStat(AgentStatIdentifiers.LOAD_PENALTY, true, 1f);
			timeScaleStat = Agent.Stats.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);
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
			shakeSource?.Dispose();

			// VFX
			agentTrailEffect.End();
		}

		private void InitiateDash()
		{
			dashTime = 0f;
			exited = false;
			SetDirection(movementHandler.InputRaw);

			// Disable default movement application from interfering.
			movementHandler.AutoUpdateMovement = false;

			// Drain stat. Physical factors only: mass (which already includes equip load) and encumberment. Deliberately
			// NOT scaled by Dash_Speed — Agility already pays out as a bigger stamina pool, so charging it here too
			// would count the same attribute twice.
			if (pointStat != null)
			{
				float cost = massStat * dashSpeed * Move.ChargeCost.Cost * 0.1f / LoadMod;
				float drained = pointStat.Drain(cost);

				// AIR: pay for the burst.
				statHandler.RewardExpPoints(Element.Air, drained, ExpSources.DASH);
			}

			// Report impact for senses / shaking.
			if (Agent.Identification.HasAll(EntityLabels.PLAYER))
			{
				shakeSource = new ContinuousShakeSource(shakeMagnitude, direction);
			}
			senseComponent.ReportImpact(new ImpactData()
			{
				Source = Agent,
				Direction = direction,
				Location = Agent.Transform.position,
				Force = 10f, // Overcome Log10
				ShakeSource = shakeSource
			});

			// Play dash SFX.
			dashSFX.Play(audioPool.Request(Agent.Transform.position, Agent.Transform).AudioSourceWrapper);

			// Start glide SFX.
			glideAudio = audioPool.Request(Agent.Transform.position, Agent.Transform).AudioSourceWrapper;
			glideSFX.PlayLoop(glideAudio, true);

			// VFX
			agentTrailEffect.Begin();
		}

		// Applies physics.
		private void OnFixedUpdate(float delta)
		{
			if (exited)
			{
				// Exit() handed movement back to the handler; applying thrust now would fight it.
				return;
			}

			// Same footing rules as entry, enforced for the whole performance: no purchase, no dash. Sliding hands over to
			// the handler's sliding model; losing ground hands over to air control instead of gliding off a ledge.
			if ((!AllowSliding && grounder.Sliding) || (RequireGrounded && !grounder.Grounded))
			{
				Exit();
				return;
			}

			// CallbackService hands out raw fixedDeltaTime; scale it locally like every other timed system.
			delta *= timeScaleStat ?? 1f;
			dashTime += delta;

			if (Bursting)
			{
				// Apply dash control. Scaled by Mobility like all other movement: the dash gets no special traction, so
				// terrain too steep to run up is too steep to dash up (and downhill even gives a boost).
				RigidbodyWrapper.ApplyMovement(direction * DashSpeed, maxAcceleration, maxDeceleration, power, true, Traction);
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
				RigidbodyWrapper.ApplyMovement(velocity, maxAcceleration, maxDeceleration, power, true, Traction);
				movementHandler.UpdateRotation(delta, null, true);
			}
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

			if (Bursting)
			{
				SetDirection(movementHandler.InputSmooth);
			}

			if (!exited && State == PerformanceState.Preparing && !Bursting && pointStat != null)
			{
				// Gliding, drain stat. Physical factors only, same reasoning as the burst cost.
				float cost = massStat * glideSpeed * Move.ChargeCost.Cost * delta * 0.1f / LoadMod;
				float spent = pointStat.Drain(cost, out bool drained);
				statHandler.RewardExpPoints(Element.Air, spent, ExpSources.DASH);
				if (drained)
				{
					// Exit dash.
					Exit();
				}
			}

			// > UPDATE FX:

			// Update shake.
			if (shakeSource != null)
			{
				shakeSource.Direction = direction;
				shakeSource.Frequency = IShakeSource.DEFAULT_FREQUENCY * RigidbodyWrapper.Acceleration.magnitude.InvertClamped();
				shakeSource.Intensity = RigidbodyWrapper.Speed.InverseLerp(movementHandler.FullSpeed, glideSpeed);
			}

			// Normalized against the BASE glide speed, not the stat-scaled target — dividing by the live target would
			// cancel the very thing being expressed and make every level sound identical.
			float intensity = Mathf.Clamp01(RigidbodyWrapper.Speed / glideSpeed);
			glideAudio.Pitch.BaseValue = glideSFX.PitchRange.Lerp(intensity);
			glideAudio.Volume.BaseValue = (dashTime / DashDuration).Clamp01() * glideSFX.VolumeRange.Lerp(intensity);
		}

		protected override IPoserInstructions Evaluate(out float weight)
		{
			Vector3 input = RigidbodyWrapper.RelativeVelocity;
			IPoserInstructions instructions = Move.PosingData.GetInstructions(Performer.ChargeTime, input);

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
			exited = true;
			movementHandler.AutoUpdateMovement = true;
			Performer.TryPerform();
		}
	}
}
