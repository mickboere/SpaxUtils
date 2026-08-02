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
		[Header("Jump Height")]
		[SerializeField, Min(0.0001f), Tooltip("Height (m) a single tap buys. Charging climbs from here.")]
		private float defaultHeight = 1.2f;

		[SerializeField, Tooltip("Stamina a default-height jump costs an agent of Reference Mass. The price knob, in points.")]
		private float defaultHeightCost = 45f;

		[SerializeField, Min(0.0001f), Tooltip("Mass (kg) the cost is quoted at. Heavier agents pay proportionally more.")]
		private float referenceMass = 100f;

		[SerializeField, Min(1f), Tooltip("Exponent on height RELATIVE to default. 1 = physically linear (no diminishing returns). Above 1 each extra metre costs more than the last, so height curves off without ever hard-capping — the ceiling is whatever the stamina pool can buy. Does not affect the default-height price.")]
		private float heightCostExponent = 2f;

		[SerializeField, Tooltip("Metres of height added per second while held. Paced by Jump_Speed via ChargeTime, so Agility reaches a given height faster rather than higher.")]
		private float heightChargeRate = 1f;

		[SerializeField, Range(0f, 1f), Tooltip("How much input direction influences the jump angle. " +
			"0 = pure surface normal, 1 = fully steered by input.")]
		private float inputInfluence = 0.3f;

		[Header("Charge Braking")]
		[SerializeField, Tooltip("How quickly the agent decelerates to a stop during charge.")]
		private float chargeBrakeDeceleration = 2000f;
		[SerializeField] private float chargeBrakePower = 50f;

		[Header("Load")]
		[SerializeField, Tooltip("Exponent applied to the LoadPenalty stat. 1 = as encumbered as general movement, higher = jumping suffers more. Raises the stamina price of a jump; height is already cut by mass in Grounder.Jump.")]
		private float loadSensitivity = 2f;

		[Header("SFX")]
		[SerializeField] private SFXData chargeSFX;
		[SerializeField] private SFXData jumpSFX;

		private GrounderComponent grounder;
		private IAgentMovementHandler movementHandler;
		private AgentStatHandler statHandler;
		private Pool<PooledAudioSource> audioPool;
		private AgentAudioHandler agentAudio;

		private PointsStat staminaStat;
		private EntityStat massStat;
		private EntityStat loadPenaltyStat;
		// Floors the rescale divisor so a near-horizontal slide normal can't blow the horizontal component up.
		private const float MIN_LAUNCH_VERTICAL = 0.25f;

		private float totalDrained;
		private bool hasJumped;
		private float landingWait;

		/// <summary>Encumberment multiplier; LoadPenalty is the shared authority, the exponent is how much jumping cares.</summary>
		private float LoadMod => Mathf.Max(0.01f, Mathf.Pow(loadPenaltyStat ?? 1f, loadSensitivity));

		/// <summary>Height being charged toward. ChargeTime is already paced by Jump_Speed.</summary>
		private float TargetHeight => defaultHeight + heightChargeRate * Performer.ChargeTime;

		private float Mass => massStat != null ? Mathf.Max(0.0001f, (float)massStat) : 1f;

		/// <summary>Price of a default-height jump for this body, before the height curve.</summary>
		private float BaseCost => defaultHeightCost * Move.ChargeCost.Cost * (Mass / referenceMass) / LoadMod;

		/// <summary>Stamina price of <paramref name="height"/>. Linear in mass, so a heavy or overloaded body doesn't jump
		/// lower directly — it just can't afford as much height.</summary>
		private float CostForHeight(float height)
		{
			return BaseCost * Mathf.Pow(Mathf.Max(0f, height) / defaultHeight, heightCostExponent);
		}

		/// <summary>Inverse of <see cref="CostForHeight"/>: the height a given spend actually bought. This is what makes a
		/// half-empty bar produce a short jump instead of a free full one.</summary>
		private float HeightForCost(float cost)
		{
			float baseCost = BaseCost;
			return baseCost <= 0f ? 0f : defaultHeight * Mathf.Pow(Mathf.Max(0f, cost) / baseCost, 1f / heightCostExponent);
		}

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
			AgentStatHandler statHandler, Pool<PooledAudioSource> audioPool, AgentAudioHandler agentAudio)
		{
			this.grounder = grounder;
			this.movementHandler = movementHandler;
			this.statHandler = statHandler;
			this.audioPool = audioPool;
			this.agentAudio = agentAudio;

			statHandler.TryGetPointStat(Move.ChargeCost.Stat, out staminaStat);
			massStat = Agent.Stats.GetStat(AgentStatIdentifiers.MASS);
			loadPenaltyStat = Agent.Stats.GetStat(AgentStatIdentifiers.LOAD_PENALTY, true, 1f);
		}

		public override void Start()
		{
			base.Start();

			totalDrained = 0f;
			hasJumped = false;
			landingWait = 0f;

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

			// Cancelled before launch: the charge bought nothing, so hand every point back. Exact refund, no gain
			// multiplier — totalDrained already records what was actually removed, multiplier included.
			if (Performer.Canceled && !hasJumped && totalDrained > 0f && staminaStat != null)
			{
				staminaStat.Gain(totalDrained, false);
				totalDrained = 0f;
			}
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

			if (State == PerformanceState.Preparing && !hasJumped && !Performer.Canceled)
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

				// Pay for the height charged toward so far. Charging opens at the default-height price, so a tap is
				// never a cheap jump, and climbs from there for as long as the bar holds out.
				if (ChargeTo(TargetHeight))
				{
					// Stamina depleted, force release.
					Performer.TryPerform();
				}
			}

			if (State == PerformanceState.Performing && !hasJumped && !Performer.Canceled)
			{
				// Charge anywhere the grounder reaches, but only LAUNCH once actually settled. This is what stops a
				// second jump firing on the way down, while still letting a jump be charged mid-descent and released the
				// instant the feet land — the frog-hop chain. Prolong holds the performance open while waiting.
				if (!RequireGrounded || grounder.Standing)
				{
					ExecuteJump();
				}
				else if (Move.RequireMinCharge)
				{
					// The move's own rule: releasing before its conditions are met kills it rather than being held.
					Performer.TryCancel(true);
				}
				else
				{
					// Held until the feet settle, matching how this flag already means "carry on rather than drop it".
					Performer.Prolong = true;

					// Don't wait forever: if the ground goes away entirely (walked off a ledge mid-release) drop it and
					// refund, using the same window the base applies to charging.
					landingWait = grounder.Grounded ? 0f : landingWait + delta;
					if (landingWait > GroundLossGrace)
					{
						Performer.TryCancel(true);
					}
				}
			}
		}

		/// <summary>
		/// Charges up to <paramref name="height"/>, drawing only the shortfall against what has already been paid.
		/// Accumulates what was ACTUALLY drained, so an empty bar simply stops the height climbing.
		/// </summary>
		/// <returns>Whether the stat ran dry.</returns>
		private bool ChargeTo(float height)
		{
			if (staminaStat == null)
			{
				return false;
			}

			float owed = CostForHeight(height) - totalDrained;
			if (owed <= 0f)
			{
				return false;
			}

			totalDrained += staminaStat.Drain(owed, out bool drained);
			return drained;
		}

		private void ExecuteJump()
		{
			hasJumped = true;
			Performer.Prolong = false;

			// Settle up before launching: a tap goes straight to Performing, so the default-height price may not have
			// been drawn yet. Whatever the bar could cover is what gets bought.
			ChargeTo(TargetHeight);

			// AIR: pay once per jump, for the stamina it cost to charge and launch.
			statHandler.RewardExpPoints(Element.Air, totalDrained, ExpSources.JUMP);

			// Height actually purchased, then v = sqrt(2gh). Multiplied by mass because Grounder.Jump divides it back
			// out — mass is expressed in the price, not a second time in the launch.
			float height = HeightForCost(totalDrained);
			float jumpForce = Mathf.Sqrt(2f * Mathf.Max(0f, grounder.Gravity) * height) * Mass;
			float chargeFraction = staminaStat != null && staminaStat.Max > 0f
				? Mathf.Clamp01(totalDrained / staminaStat.Max)
				: 0f;

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

			// Apply jump through grounder. Rescaled so the VERTICAL component always equals what the purchased height
			// needs: steering adds horizontal on top instead of trading altitude for it, so a running or directional
			// jump reaches the same apex as a standing one. Grounder.Jump caps the horizontal against sprint speed.
			Vector3 launch = jumpDirection.normalized;
			grounder.Jump(launch / Mathf.Max(MIN_LAUNCH_VERTICAL, launch.y) * jumpForce);

			// Re-enable movement for air control.
			movementHandler.AutoUpdateMovement = true;

			// Play jump SFX.
			if (jumpSFX != null && audioPool != null)
			{
				jumpSFX.Play(audioPool.Request(Agent.Transform.position, Agent.Transform).AudioSourceWrapper);
			}

			// Play exertion SFX.
			agentAudio.PlayExertion(chargeFraction.OutSine());
		}
	}
}
