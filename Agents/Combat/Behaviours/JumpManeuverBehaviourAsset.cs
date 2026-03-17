using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="BaseCombatMoveBehaviourAsset"/> that executes a chargeable jump.
	/// Hold to squat and charge (drains stamina), release to jump.
	/// Jump force scales with stamina drained. Direction blends surface normal with input.
	/// Passes jump execution to <see cref="GrounderComponent.Jump"/> and completes.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(JumpManeuverBehaviourAsset), menuName = "Performance/Behaviour/" + nameof(JumpManeuverBehaviourAsset))]
	public class JumpManeuverBehaviourAsset : BaseCombatMoveBehaviourAsset
	{
		[Header("Jump Force")]
		[SerializeField, Tooltip("Minimum jump force applied on tap (no charge). Divided by mass for velocity.")]
		private float minForce = 500f;

		[SerializeField, Tooltip("Maximum jump force applied at full charge. Divided by mass for velocity.")]
		private float maxForce = 1200f;

		[SerializeField, Range(0f, 1f), Tooltip("How much input direction influences the jump angle. " +
			"0 = pure surface normal, 1 = fully steered by input.")]
		private float inputInfluence = 0.3f;

		[Header("Charge Braking")]
		[SerializeField, Tooltip("How quickly the agent decelerates to a stop during charge.")]
		private float chargeBrakeDeceleration = 2000f;
		[SerializeField] private float chargeBrakePower = 50f;

		[Header("SFX")]
		[SerializeField] private SFXData chargeSFX;
		[SerializeField] private SFXData jumpSFX;

		private GrounderComponent grounder;
		private IAgentMovementHandler movementHandler;
		private AgentStatHandler statHandler;
		private Pool<PooledAudioSource> audioPool;

		private PointsStat staminaStat;
		private EntityStat massStat;
		private float totalDrained;
		private bool hasJumped;

		public override bool IsMet(IDependencyManager dependencies)
		{
			if (!base.IsMet(dependencies))
			{
				return false;
			}

			return dependencies.TryGet(out AgentStatHandler statHandler) &&
				!statHandler.PointStats.E.IsRecoveringFromZero;
		}

		public void InjectDependencies(GrounderComponent grounder, IAgentMovementHandler movementHandler,
			AgentStatHandler statHandler, Pool<PooledAudioSource> audioPool)
		{
			this.grounder = grounder;
			this.movementHandler = movementHandler;
			this.statHandler = statHandler;
			this.audioPool = audioPool;

			statHandler.TryGetPointStat(Move.ChargeCost.Stat, out staminaStat);
			massStat = Agent.Stats.GetStat(AgentStatIdentifiers.MASS);
		}

		public override void Start()
		{
			base.Start();

			totalDrained = 0f;
			hasJumped = false;

			// Disable default movement while charging, unless sliding.
			if (!grounder.Sliding)
			{
				movementHandler.AutoUpdateMovement = false;
			}

			// Play charge SFX.
			if (chargeSFX != null && audioPool != null)
			{
				chargeSFX.Play(audioPool.Request(Agent.Transform.position, Agent.Transform).AudioSourceWrapper);
			}
		}

		public override void Stop()
		{
			base.Stop();
			movementHandler.AutoUpdateMovement = true;
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

			if (State == PerformanceState.Preparing && !hasJumped)
			{
				// Brake to a stop during charge, but not while sliding.
				if (!grounder.Sliding)
				{
					RigidbodyWrapper.ApplyMovement(
						Vector3.zero,
						0f,
						chargeBrakeDeceleration,
						chargeBrakePower,
						true);
				}

				// Drain stamina per frame.
				if (staminaStat != null)
				{
					float cost = (massStat != null ? (float)massStat : 1f) * Move.ChargeCost.Cost * delta;
					staminaStat.Drain(cost, out bool drained);
					totalDrained += cost;

					if (drained)
					{
						// Stamina depleted, force release.
						Performer.TryPerform();
					}
				}
			}

			if (State == PerformanceState.Performing && !hasJumped)
			{
				ExecuteJump();
			}
		}

		private void ExecuteJump()
		{
			hasJumped = true;

			// Calculate charge fraction from stamina drained.
			float maxChargeCost = Move.MaxCharge * Move.ChargeCost.Cost * (massStat != null ? (float)massStat : 1f);
			float chargeFraction = maxChargeCost > 0f ? Mathf.Clamp01(totalDrained / maxChargeCost) : 0f;

			// Interpolate force from min to max based on charge.
			float jumpForce = Mathf.Lerp(minForce, maxForce, chargeFraction);

			// Direction: blend surface/terrain normal with input direction.
			Vector3 baseDirection = grounder.Sliding ? grounder.TerrainNormal : Vector3.up;
			Vector3 inputDir = Vector3.zero;
			if (movementHandler.InputSmooth.sqrMagnitude > 0.01f)
			{
				inputDir = Quaternion.LookRotation(movementHandler.InputAxis) * movementHandler.InputSmooth.normalized;

				// When sliding, strip the uphill component from input so the player
				// can steer laterally and downhill but can't jump up the slope.
				if (grounder.Sliding)
				{
					Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, grounder.TerrainNormal).normalized;
					Vector3 uphill = -downhill;
					float uphillDot = Vector3.Dot(inputDir, uphill);
					if (uphillDot > 0f)
					{
						inputDir -= uphill * uphillDot;
					}
				}
			}

			Vector3 jumpDirection;
			if (inputDir.sqrMagnitude > 0.01f)
			{
				jumpDirection = Vector3.Slerp(baseDirection, (baseDirection + inputDir).normalized, inputInfluence);
			}
			else
			{
				jumpDirection = baseDirection;
			}

			// Apply jump through grounder.
			grounder.Jump(jumpDirection.normalized * jumpForce);

			// Re-enable movement for air control.
			movementHandler.AutoUpdateMovement = true;

			// Play jump SFX.
			if (jumpSFX != null && audioPool != null)
			{
				jumpSFX.Play(audioPool.Request(Agent.Transform.position, Agent.Transform).AudioSourceWrapper);
			}
		}
	}
}
