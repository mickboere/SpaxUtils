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
				}
			}
		}
		private Vector3 _inputRaw;

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
		[field: SerializeField] public float MinSpeed { get; set; } = 1f;
		[field: SerializeField] public float HalfSpeed { get; set; } = 1.5f;
		[field: SerializeField] public float FullSpeed { get; set; } = 4.5f;

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
		[SerializeField] protected float rotationSmoothing = 30f;

		[Header("Stats")]
		[SerializeField, Range(0f, 1f)] protected float velocityRecoveryMod = 0.5f;
		[SerializeField] protected float sprintCost = 0.25f;
		[SerializeField] protected float tiredInputLimiter = 0.75f;

		[Header("Sliding")]
		[SerializeField] protected float slideSteeringSpeed = 4f;
		[SerializeField, Range(0f, 1f)] protected float slideSpeedSteerDamp = 0.2f;

		[Header("Debugging")]
		[SerializeField] protected bool debug;

		protected RigidbodyWrapper rigidbodyWrapper;
		protected GrounderComponent grounder;
		protected MovementInputSettings inputSettings;
		protected AgentStatHandler statHandler;

		protected Vector3 processedInput;
		protected MovementInputHelper inputHelper;
		protected EntityStat moveSpeedStat;
		protected EntityStat recoveryStat;
		protected FloatOperationModifier recoveryMod;

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
			recoveryStat = Agent.Stats.GetStat(AgentStatIdentifiers.RECOVERY, true, 1f);
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
			// Calculate appropriate input value according to stats.
			processedInput =
				statHandler.PointStats.E.IsRecoveringFromZero && InputRaw != Vector3.zero
					? InputRaw.ClampMagnitude(tiredInputLimiter)
					: InputRaw;

			// Update smooth input value.
			InputSmooth = inputHelper.Update(processedInput, Time.deltaTime);

			// Slow down recovery while running.
			recoveryMod.SetValue(
				1f - Mathf.InverseLerp(HalfSpeed, FullSpeed * moveSpeedStat, rigidbodyWrapper.Speed)
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

			if (!targetVelocity.HasValue)
			{
				// Calculate target velocity from current input.
				rigidbodyWrapper.TargetVelocity = InputSmooth == Vector3.zero
					? Vector3.zero
					: Quaternion.LookRotation(InputAxis) *
					  InputSmooth.normalized *
					  CalculateSpeed(InputSmooth.magnitude);
			}

			if (grounder.Grounded)
			{
				if (!grounder.Sliding)
				{
					// Default movement control.
					float acFalloff = accelerationFalloff.Evaluate(rigidbodyWrapper.Speed * rigidbodyWrapper.Control / FullSpeed);
					float deFalloff = decelerationFalloff.Evaluate(rigidbodyWrapper.Speed * rigidbodyWrapper.Control / FullSpeed);
					rigidbodyWrapper.ApplyMovement(
						targetVelocity,
						maxAcceleration * acFalloff,
						maxDeceleration * deFalloff,
						power,
						ignoreControl,
						grounder.Mobility);

					if (processedInput.magnitude > 1.01f)
					{
						// Apply sprint cost.
						statHandler.PointStats.E.Drain(
							sprintCost * rigidbodyWrapper.Mass *
							(rigidbodyWrapper.Speed / (FullSpeed * 1.5f * moveSpeedStat)) *
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
							target * moveSpeedStat * slideSteeringSpeed * scale,
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

			if (debug)
			{
				SpaxDebug.Log($"[{Agent.ID}]", $"InputRaw={InputRaw}, InputSmooth={InputSmooth}, target={rigidbodyWrapper.TargetVelocity}");
			}
		}

		/// <inheritdoc/>
		public virtual void UpdateRotation(float delta, Vector3? targetDirection = null, bool ignoreControl = false)
		{
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
						// and at target velocity direction when at 0% grip.
						Vector3 a = flatVelocity.normalized;
						Vector3 b = flatTargetVel.normalized;
						Turn(
							a.Slerp(b, rigidbodyWrapper.Grip.InvertClamped().InOutQuint()),
							a.NormalizedDot(b).InOutSine());
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
			return input < inputSettings.MinimumInput
				? MinSpeed * (input / inputSettings.MinimumInput)
				: input < 0.5f
					? MinSpeed.Lerp(HalfSpeed, input * 2f)
					: input < 1f
						? HalfSpeed.Lerp(FullSpeed * moveSpeedStat, (input - 0.5f) * 2f)
						: FullSpeed * moveSpeedStat * input;
		}
	}
}
