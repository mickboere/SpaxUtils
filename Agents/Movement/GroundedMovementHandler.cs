using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	[DefaultExecutionOrder(30)]
	public class GroundedMovementHandler : AgentComponentBase, IAgentMovementHandler
	{
		/// <inheritdoc/>
		public Vector3 InputAxis
		{
			get { return _inputAxis; }
			set
			{
				if (float.IsNaN(value.x))
				{
					SpaxDebug.Log("NaN!");
				}
				else if (value == Vector3.zero)
				{
					SpaxDebug.Error("Input axis cannot be zero.");
				}
				else
				{
					_inputAxis = value.FlattenY().normalized;
				}
			}
		}
		private Vector3 _inputAxis = Vector3.forward;

		/// <inheritdoc/>
		public Vector3 InputRaw
		{
			get { return _inputRaw; }
			set
			{
				if (float.IsNaN(value.x))
				{
					SpaxDebug.Log("NaN!");
				}
				else
				{
					_inputRaw = value;
					lastInputTime = Time.time;
				}
			}
		}
		private Vector3 _inputRaw;
		private float lastInputTime;

		/// <inheritdoc/>
		public Vector3 InputSmooth { get { return _inputSmooth; } set { _inputSmooth = value; } }
		private Vector3 _inputSmooth;

		/// <inheritdoc/>
		public Vector3 TargetDirection
		{
			get { return _forwardDirection; }
			set { _forwardDirection = value == Vector3.zero ? Transform.forward : value.FlattenY().normalized; }
		}
		private Vector3 _forwardDirection;

		/// <inheritdoc/>
		public bool LockRotation { get; set; }

		/// <inheritdoc/>
		public bool AutoUpdateMovement { get; set; } = true;

		/// <inheritdoc/>
		public bool AutoUpdateRotation { get; set; } = true;

		[Header("Movement")]
		[field: SerializeField, Tooltip("Speed at minimum input magnitude.")]
		public float MinSpeed { get; set; } = 1f;
		[field: SerializeField, Tooltip("Speed at half (0.5) input magnitude.")]
		public float HalfSpeed { get; set; } = 1.5f;
		[field: SerializeField, Tooltip("Speed at full (1) input magnitude, scaled by the movement speed stat.")]
		public float FullSpeed { get; set; } = 4.5f;
		[SerializeField, Tooltip("Safety net: if no input is written for this many seconds, InputRaw auto-resets to zero. Catches behaviours that stop driving movement without zeroing it (e.g. on state exit).")]
		protected float inputTimeout = 0.3f;

		/// <inheritdoc/>
		public float MinimumInput => inputSettings.MinimumInput;

		[Header("Physics")]
		[SerializeField, Tooltip("The max amount of force that can be applied to reach the desired velocity.")]
		protected float maxAcceleration = 2500f;
		[SerializeField, Tooltip("The falloff curve of maxForce over current relative movement speed."), FormerlySerializedAs("controlFalloff")]
		protected AnimationCurve accelerationFalloff = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1.5f, 0.333f));
		[SerializeField, Tooltip("The max amount of force that can be applied to reach the desired velocity.")]
		protected float maxDeceleration = 1500f;
		[SerializeField, Tooltip("The falloff curve of maxForce over current relative movement speed.")]
		protected AnimationCurve decelerationFalloff = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(3f, 3f));
		[SerializeField, Tooltip("General force responsiveness.")]
		protected float power = 50f;

		[Header("Rotation")]
		[SerializeField, Tooltip("Rotation turn-rate smoothing; higher is snappier.")] protected float rotationSmoothing = 30f;

		[Header("Stats")]
		[SerializeField, Range(0f, 1f), Tooltip("How much running slows recovery-stat regen. 0 = no slowdown, 1 = full stop at top speed.")] protected float velocityRecoveryMod = 0.5f;
		[SerializeField, Tooltip("Endurance drained per second while sprinting, scaled by mass, relative speed and grip.")] protected float sprintCost = 0.25f;
		[SerializeField, Tooltip("Input magnitude cap applied while recovering from zero endurance.")] protected float tiredInputLimiter = 0.75f;
		[SerializeField, Tooltip("Rate at which sprint speed builds up toward max sprint speed (0..1 per second).")]
		protected float sprintRampRate = 0.5f;
		[SerializeField, Tooltip("Rate at which sprint buildup decays when not sprinting (0..1 per second).")]
		protected float sprintRampDownRate = 2f;
		[SerializeField, Tooltip("Scales how much effective load reduces sprint buildup rate, sprint acceleration, and turn-rate smoothing. loadSpeedMod = 1 / (1 + effectiveLoad * factor). Load beyond the LoadCapacity stat is what counts.")]
		protected float loadPenaltyFactor = 0.01f;
		[SerializeField, Tooltip("Maximum rate (m/s per second) at which TargetVelocity tracks the desired velocity when under load. Scales down further with loadSpeedMod.")]
		protected float targetVelocityTurnRate = 15f;
		[SerializeField, Tooltip("Effective load (kg) range for turn-rate smoothing. Below x: instant snap. At y: full smoothing effect."), MinMaxRange(0f, 100f, false)]
		protected Vector2 turnSmoothingRange = new Vector2(10f, 50f);

		[Header("Sliding")]
		[SerializeField, Tooltip("Lateral steering speed while sliding on a slope.")] protected float slideSteeringSpeed = 4f;
		[SerializeField, Range(0f, 1f), Tooltip("Damps slide steering by vertical speed. 0 = no damping, 1 = full.")] protected float slideSpeedSteerDamp = 0.2f;

		[Header("Air Control")]
		[SerializeField, Tooltip("Maximum air control force. Actual force is scaled by the agent's air control stat.")]
		protected float airControlForce = 500f;
		[SerializeField, Tooltip("Air control responsiveness.")]
		protected float airControlPower = 10f;
		[SerializeField, ConstDropdown(typeof(IStatIdentifiers)), Tooltip("Stat that scales air control. 0 = no control, 1 = full control.")]
		protected string airControlStat;

		[Header("Debugging")]
		[SerializeField, Tooltip("Log movement debug info to the console.")] protected bool debug;

		protected RigidbodyWrapper rigidbodyWrapper;
		protected GrounderComponent grounder;
		protected MovementInputSettings inputSettings;
		protected AgentStatHandler statHandler;

		protected Vector3 processedInput;
		protected MovementInputHelper inputHelper;
		protected EntityStat moveSpeedStat;
		protected EntityStat sprintSpeedStat;
		protected EntityStat recoveryStat;
		protected EntityStat airControlStatValue;
		protected EntityStat loadStat;
		protected EntityStat loadCapacityStat;
		protected FloatOperationModifier recoveryMod;
		private float sprintBuildup;
		private float loadSpeedMod = 1f;

		/// <summary>
		/// Equip load (kg) beyond what the body can carry. Capacity is a stat in its own right, so what feeds
		/// it (Strength, Poise, gear) is decided by the stat maps rather than here.
		/// </summary>
		protected float EffectiveLoad => Mathf.Max(0f, (float)loadStat - (float)loadCapacityStat);

		/// <summary>
		/// Movement penalty from carrying more than you can: scales sprint buildup, acceleration and turn rate
		/// (and inversely, sprint decay and the planted brake). Never touches top speed.
		/// </summary>
		protected float LoadSpeedMod => 1f / (1f + EffectiveLoad * loadPenaltyFactor);

		public void InjectDependencies(
			RigidbodyWrapper rigidbodyWrapper,
			GrounderComponent grounder,
			MovementInputSettings inputSettings,
			AgentStatHandler statHandler)
		{
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.grounder = grounder;
			this.inputSettings = inputSettings;
			this.statHandler = statHandler;

			moveSpeedStat = Agent.Stats.GetStat(AgentStatIdentifiers.MOVEMENT_SPEED, true, 1f);
			sprintSpeedStat = Agent.Stats.GetStat(AgentStatIdentifiers.SPRINT_SPEED, true, 1f);
			recoveryStat = Agent.Stats.GetStat(AgentStatIdentifiers.RECOVERY, true, 1f);
			loadStat = Agent.Stats.GetStat(AgentStatIdentifiers.LOAD, true, 0f);
			loadCapacityStat = Agent.Stats.GetStat(AgentStatIdentifiers.LOAD_CAPACITY, true, 0f);
			if (!string.IsNullOrEmpty(airControlStat))
			{
				airControlStatValue = Agent.Stats.GetStat(airControlStat, true, 0f);
			}
		}

		protected void OnEnable()
		{
			inputHelper = new MovementInputHelper(inputSettings);
			InputAxis = Transform.forward;
			TargetDirection = Transform.forward;
			recoveryMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			recoveryStat.AddModifier(this, recoveryMod);
		}

		protected void OnDisable()
		{
			inputHelper.Dispose();
			recoveryStat.RemoveModifier(recoveryMod);
			recoveryMod.Dispose();
		}

		protected void Update()
		{
			// Safety net: if nothing has driven input for inputTimeout seconds, force a stop. Prevents agents running
			// off when a movement behaviour stops writing input without zeroing it (e.g. on brain-state exit).
			if (_inputRaw != Vector3.zero && Time.time - lastInputTime > inputTimeout)
			{
				_inputRaw = Vector3.zero;
			}

			// Calculate appropriate input value according to stats.
			processedInput =
				statHandler.PointStats.E.IsRecoveringFromZero && InputRaw != Vector3.zero
					? InputRaw.ClampMagnitude(tiredInputLimiter)
					: InputRaw;

			// Update smooth input value.
			InputSmooth = inputHelper.Update(processedInput, Time.deltaTime);

			// Slow down recovery while running; full slowdown only at sprint top speed.
			recoveryMod.SetValue(
				1f - Mathf.InverseLerp(HalfSpeed, FullSpeed * 1.5f * sprintSpeedStat * moveSpeedStat, rigidbodyWrapper.Speed)
					.InOutSine() * velocityRecoveryMod);
		}

		protected void FixedUpdate()
		{
			if (AutoUpdateMovement)
			{
				UpdateMovement(Time.fixedDeltaTime * EntityTimeScale);
			}
			if (AutoUpdateRotation)
			{
				UpdateRotation(Time.fixedDeltaTime * EntityTimeScale);
			}
		}

		/// <inheritdoc/>
		public virtual void UpdateMovement(float delta, Vector3? targetVelocity = null, bool ignoreControl = false)
		{
			// Grounded movement does not utilize Y axis.
			rigidbodyWrapper.ControlAxis = Vector3.one.FlattenY();

			bool isSprinting = processedInput.magnitude > 1.01f;

			// Load above capacity incurs a movement penalty: slower sprint buildup, acceleration, and turning.
			float effectiveLoad = EffectiveLoad;
			loadSpeedMod = LoadSpeedMod;

			// Heavy load slows sprint buildup; friction (inverse load) accelerates decay back to walk speed.
			sprintBuildup = isSprinting
				? Mathf.MoveTowards(sprintBuildup, 1f, sprintRampRate * loadSpeedMod * delta)
				: Mathf.MoveTowards(sprintBuildup, 0f, sprintRampDownRate * (1f / loadSpeedMod) * delta);

			if (!targetVelocity.HasValue)
			{
				// Treat anything below MinimumInput as a full stop. == Vector3.zero was too strict: the SmoothDamp
				// tail lingers just above zero, and normalizing that tiny noisy vector yields a wildly swinging
				// direction (the in-place "indecisive" jitter) that the turnRate then snaps TargetVelocity onto.
				Vector3 desiredTarget = InputSmooth.magnitude < inputSettings.MinimumInput
					? Vector3.zero
					: Quaternion.LookRotation(InputAxis) *
					  InputSmooth.normalized *
					  CalculateSpeed(InputSmooth.magnitude);

				// Smooth TargetVelocity under heavy load to prevent grip spikes during sharp turns.
				// turnSmoothing ramps from 0 (instant) at the threshold to 1 (full effect) at 2x the threshold.
				// Rate uses a reciprocal so onset is gradual rather than a hard switch.
				float turnSmoothing = Mathf.InverseLerp(turnSmoothingRange.x, turnSmoothingRange.y, effectiveLoad);
				float turnRate = targetVelocityTurnRate * loadSpeedMod / Mathf.Max(turnSmoothing, 0.01f);
				rigidbodyWrapper.TargetVelocity = Vector3.MoveTowards(rigidbodyWrapper.TargetVelocity, desiredTarget, turnRate * delta);
			}

			if (grounder.Grounded)
			{
				if (!grounder.Sliding)
				{
					// Default movement control.
					float acFalloff = accelerationFalloff.Evaluate(rigidbodyWrapper.Speed * rigidbodyWrapper.Control / FullSpeed);
					float deFalloff = decelerationFalloff.Evaluate(rigidbodyWrapper.Speed * rigidbodyWrapper.Control / FullSpeed);
					// Load reduces acceleration and free-movement deceleration (inertia).
					// maxBrake uses the inverse so planted stops (control=0) are stronger under load.
					rigidbodyWrapper.ApplyMovement(
						targetVelocity,
						maxAcceleration * acFalloff * loadSpeedMod,
						maxDeceleration * deFalloff * loadSpeedMod,
						power,
						ignoreControl,
						grounder.Mobility,
						maxDeceleration * deFalloff * (1f / loadSpeedMod));

					if (processedInput.magnitude > 1.01f)
					{
						// Apply sprint cost.
						statHandler.PointStats.E.Drain(
							sprintCost * rigidbodyWrapper.Mass *
							(rigidbodyWrapper.Speed / (FullSpeed * 1.5f * sprintSpeedStat * moveSpeedStat)) *
							rigidbodyWrapper.Control * delta);
					}
				}
				else
				{
					float terrainAngle = Vector3.Angle(Vector3.up, grounder.TerrainNormal);

					// Braking authority: high on flat ground, zero at friction angle.
					float brakingAuthority = Mathf.Clamp01(1f - terrainAngle / grounder.StaticFrictionAngle);

					if (terrainAngle > 1f)
					{
						// On a slope: lateral steering perpendicular to the downhill direction.
						Vector3 right = Vector3.Cross(Vector3.up, grounder.TerrainNormal);
						Vector3 downhill = right.Cross(grounder.TerrainNormal);
						Quaternion downQ = Quaternion.LookRotation(downhill, grounder.TerrainNormal).Inverse();
						float current = (downQ * rigidbodyWrapper.Velocity).x;
						float target = (downQ * (Quaternion.LookRotation(InputAxis) * InputSmooth).ProjectOnPlane(downhill)).x;
						float scale = (rigidbodyWrapper.Velocity.y * slideSpeedSteerDamp).Abs().Clamp01().InOutSine();
						Vector3 force = right * current.CalculateForce(
							target * sprintSpeedStat * moveSpeedStat * slideSteeringSpeed * scale,
							power * EntityTimeScale * scale,
							maxAcceleration * EntityTimeScale * scale);
						rigidbodyWrapper.AddForce(force);
					}

					// Braking only. No acceleration - gravity handles downslope speed.
					// Clamp input to non-sprint magnitude so sprint cannot sustain a slide.
					if (brakingAuthority > 0.01f)
					{
						Vector3 clampedInput = InputSmooth.ClampMagnitude(1f);
						Vector3 brakeTarget = clampedInput == Vector3.zero
							? Vector3.zero
							: Quaternion.LookRotation(InputAxis) *
							  clampedInput.normalized *
							  CalculateSpeed(clampedInput.magnitude);
						rigidbodyWrapper.ApplyMovement(
							brakeTarget,
							0f,
							maxDeceleration * brakingAuthority,
							power * brakingAuthority,
							ignoreControl);
					}
				}
			}
			else if (airControlStatValue != null && airControlStatValue > 0.01f && InputSmooth.sqrMagnitude > 0.01f)
			{
				// Airborne: apply air control force toward input direction, scaled by stat.
				Vector3 airTarget = Quaternion.LookRotation(InputAxis) * InputSmooth.ClampMagnitude(1f) * FullSpeed * sprintSpeedStat * moveSpeedStat;
				float control = Mathf.Clamp01(airControlStatValue);
				rigidbodyWrapper.ApplyMovement(
					airTarget,
					airControlForce * control,
					airControlForce * control * 0.5f,
					airControlPower * control,
					true);
			}

			if (debug)
			{
				SpaxDebug.Log($"[{Agent.ID}]", $"InputRaw={InputRaw}, InputSmooth={InputSmooth}, target={rigidbodyWrapper.TargetVelocity}");
			}
		}

		/// <inheritdoc/>
		public virtual void UpdateRotation(float delta, Vector3? targetDirection = null, bool ignoreControl = false)
		{
			if (debug)
			{
				SpaxDebug.Log($"[{Agent.ID}]", $"UpdateRotation: LockRotation={LockRotation}, TargetDirection={TargetDirection}, Sliding={grounder.Sliding}, Control={rigidbodyWrapper.Control}, delta={delta}");
			}

			float time = delta * (ignoreControl ? 1f : rigidbodyWrapper.Control);

			if (!targetDirection.HasValue)
			{
				Vector3 flatVelocity = rigidbodyWrapper.Velocity.FlattenY();

				if (grounder.Sliding)
				{
					// Turn towards velocity direction.
					Turn(flatVelocity.normalized);
				}
				else if (LockRotation && TargetDirection != Vector3.zero)
				{
					// Lock rotation in set target direction.
					Turn(TargetDirection);
				}
				else
				{
					Vector3 flatTargetVel = rigidbodyWrapper.TargetVelocity.FlattenY();
					if (!(rigidbodyWrapper.TargetVelocity == Vector3.zero || flatVelocity == Vector3.zero))
					{
						// Rotation isn't locked, look in velocity direction when at 100% grip
						// and at target velocity direction when at 0% grip. Rotation is HELD entirely while the input
						// is reversing — raw has flipped but the smoothed input (which velocity chases) still points
						// the old way, so Dot(InputRaw, InputSmooth) < 0 — so the body doesn't whip around to chase the
						// doomed old-direction velocity. Self-clears the instant smooth catches up to raw, and never
						// triggers while merely circling (raw and smooth stay aligned there) → no moonwalk.
						Vector3 a = flatVelocity.normalized;
						Vector3 b = flatTargetVel.normalized;
						Turn(
							a.Slerp(b, rigidbodyWrapper.Grip.InvertClamped().InOutQuint()),
							a.NormalizedDot(b).InOutSine() * (Vector3.Dot(InputRaw, InputSmooth) < 0f ? 0f : 1f));
					}
				}
			}
			else if (targetDirection.Value != Vector3.zero)
			{
				// Turn towards target direction.
				Turn(targetDirection.Value);
			}

			void Turn(Vector3 dir, float speed = 1f)
			{
				if (dir == Vector3.zero)
				{
					return;
				}

				rigidbodyWrapper.Rotation = rigidbodyWrapper.Rotation.FISlerp(
					Quaternion.LookRotation(dir),
					speed * rotationSmoothing * time);
			}
		}

		/// <inheritdoc/>
		public void ForceRotation(Vector3? direction = null, float maxTurn = 1f)
		{
			if ((!direction.HasValue && rigidbodyWrapper.TargetVelocity == Vector3.zero) ||
				(direction.HasValue && direction.Value == Vector3.zero))
			{
				return;
			}

			Quaternion target = direction.HasValue
				? Quaternion.LookRotation(direction.Value, Vector3.up)
				: Quaternion.LookRotation(rigidbodyWrapper.TargetVelocity.FlattenY(), Vector3.up);

			float maxDegrees = Mathf.Clamp01(maxTurn) * 180f;
			Entity.GameObject.transform.rotation = Quaternion.RotateTowards(Entity.GameObject.transform.rotation, target, maxDegrees);
		}

		/// <inheritdoc/>
		public float CalculateSpeed(float input)
		{
			// SPRINT_SPEED shapes the top speed; MOVEMENT_SPEED is the overall multiplier applied to every tier.
			float sprint = (float)sprintSpeedStat;
			float speed =
				input < inputSettings.MinimumInput
					? MinSpeed * (input / inputSettings.MinimumInput)
					: input < 0.5f
						? MinSpeed.Lerp(HalfSpeed, input * 2f)
						: input < 1f
							? HalfSpeed.Lerp(FullSpeed * Mathf.Min(1f, sprint), (input - 0.5f) * 2f)
							// The sprintBuildup=0 (walking) anchor MUST equal the [0.5,1) branch's top — FullSpeed × Min(1,
							// sprint) — NOT FullSpeed × 1. With 1f, crossing |input|=1 silently drops SPRINT_SPEED and the
							// speed snaps to full. The Lerp then ramps toward the sprint speed (sprint × input) as sprintBuildup rises.
							: FullSpeed * Mathf.Lerp(Mathf.Min(1f, sprint), sprint * input, sprintBuildup);
			return speed * moveSpeedStat;
		}

		/// <inheritdoc/>
		public float PredictBrakingDistance(float speed)
		{
			if (speed <= 0f)
			{
				return 0f;
			}

			// A performed act sets Control = 0, so ApplyMovement brakes toward zero at a force capped to
			// maxBrake · Mobility (see UpdateMovement → ApplyMovement) → effectively constant deceleration.
			// maxBrake = maxDeceleration · deFalloff(0) / loadSpeedMod (planted stops are stronger under load).
			float maxBrake = maxDeceleration * decelerationFalloff.Evaluate(0f) / LoadSpeedMod;
			float mass = Mathf.Max(rigidbodyWrapper.Mass, 0.0001f);
			float decel = maxBrake * grounder.Mobility / mass;
			return decel > 0.0001f ? (speed * speed) / (2f * decel) : 0f;
		}

		/// <inheritdoc/>
		public float PredictStoppingDistance(float speed)
		{
			if (speed <= 0f)
			{
				return 0f;
			}

			// Free-movement deceleration (Control = 1: still steering, not planted). This is the "inertia" term
			// ApplyMovement caps decel at while moving = maxDeceleration · deFalloff(speed/FullSpeed) · loadSpeedMod
			// — note load WEAKENS it (·lsm), opposite of the planted brake. deFalloff rises with speed, so decel
			// is not constant: integrate d = ∫ v/a(v) dv numerically (midpoint) so it tracks the actual curve.
			float mass = Mathf.Max(rigidbodyWrapper.Mass, 0.0001f);
			float baseDecel = maxDeceleration * LoadSpeedMod * grounder.Mobility / mass; // a(v) = baseDecel · deFalloff(v/FullSpeed)
			if (baseDecel <= 0.0001f)
			{
				return 0f;
			}

			float fullSpeed = Mathf.Max(FullSpeed, 0.01f);
			const int steps = 12;
			float dv = speed / steps;
			float distance = 0f;
			for (int i = 0; i < steps; i++)
			{
				float v = speed - (i + 0.5f) * dv; // midpoint speed of this velocity slice
				float a = baseDecel * Mathf.Max(0.0001f, decelerationFalloff.Evaluate(v / fullSpeed));
				distance += v / a * dv; // dx = v · dt = v · (dv / a)
			}
			return distance;
		}
	}
}
