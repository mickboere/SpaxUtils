using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
			get
			{
				return _forwardDirection;
			}
			set
			{
				_forwardDirection = value == Vector3.zero ? Transform.forward : value.FlattenY().normalized;
			}
		}
		private Vector3 _forwardDirection;

		/// <inheritdoc/>
		public bool LockRotation { get; set; }

		[Header("Movement")]
		[field: SerializeField] public float MinSpeed { get; set; } = 1f;
		[field: SerializeField] public float HalfSpeed { get; set; } = 1.5f;
		[field: SerializeField] public float FullSpeed { get; set; } = 4.5f;
		[Header("Physics")]
		[SerializeField, Tooltip("Movement force at 100% control.")] private float controlForce = 1800f;
		[SerializeField] private AnimationCurve controlFalloff = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1.5f, 0.333f));
		[SerializeField, Tooltip("Movement force at 0% control.")] private float brakeForce = 900f;
		[SerializeField, Tooltip("Movement power.")] private float power = 40f;
		[Header("Rotation")]
		[SerializeField] private float rotationSmoothing = 30f;
		[Header("Stats")]
		[SerializeField] private StatCost sprintCost;
		[SerializeField] private float tiredInputLimiter = 0.75f;
		[Header("Debugging")]
		[SerializeField] private bool debug;

		private RigidbodyWrapper rigidbodyWrapper;
		private IGrounderComponent grounder;
		private MovementInputSettings inputSettings;
		private MovementInputHelper inputHelper;

		private EntityStat moveSpeedStat;

		public void InjectDependencies(RigidbodyWrapper rigidbodyWrapper, IGrounderComponent grounder, MovementInputSettings inputSettings)
		{
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.grounder = grounder;
			this.inputSettings = inputSettings;

			moveSpeedStat = Agent.Stats.GetStat(AgentStatIdentifiers.MOVEMENT_SPEED, true, 1f);
		}

		protected void OnEnable()
		{
			inputHelper = new MovementInputHelper(inputSettings);
			InputAxis = Transform.forward;
			TargetDirection = Transform.forward;
		}

		protected void OnDisable()
		{
			inputHelper.Dispose();
		}

		protected void FixedUpdate()
		{
			UpdateMovement(Time.fixedDeltaTime * EntityTimeScale);
			UpdateRotation(Time.fixedDeltaTime * EntityTimeScale);
		}

		/// <inheritdoc/>
		public void UpdateMovement(float delta, Vector3? targetVelocity = null, bool ignoreControl = false)
		{
			// Grounded movement does not utilize Y axis.
			rigidbodyWrapper.ControlAxis = Vector3.one.FlattenY();

			// Calculate appropriate input value according to stats.
			Vector3 input = Entity.Stats.TryGetStat(sprintCost.Stat, out EntityStat cost) ?
					(cost > 0f || InputRaw == Vector3.zero ? InputRaw : InputRaw.ClampMagnitude(tiredInputLimiter)) :
					InputRaw;
			// Update smooth input value.
			InputSmooth = inputHelper.Update(input, delta);

			if (!targetVelocity.HasValue)
			{
				// Calculate target velocity from current input.
				rigidbodyWrapper.TargetVelocity = InputSmooth == Vector3.zero ? Vector3.zero :
					Quaternion.LookRotation(InputAxis) * InputSmooth.normalized * CalculateSpeed(InputSmooth.magnitude) * moveSpeedStat;
			}

			if (!grounder.Sliding)
			{
				rigidbodyWrapper.ApplyMovement(targetVelocity, controlForce * controlFalloff.Evaluate(rigidbodyWrapper.Speed / FullSpeed),
					brakeForce, power, ignoreControl, grounder.Mobility);

				if (InputRaw.magnitude > 1.01f)
				{
					Entity.Stats.TryApplyStatCost(sprintCost.Stat, sprintCost.Cost * rigidbodyWrapper.Mass * (InputRaw.magnitude - 1f) * delta);
				}
			}
			//else
			//{
			// TODO: Allow for sliding control & overhaul sliding altogether.
			//}

			if (debug)
			{
				SpaxDebug.Log($"[{Agent.ID}]", $"InputRaw={InputRaw}, InputSmooth={InputSmooth}, target={rigidbodyWrapper.TargetVelocity}");
			}
		}

		/// <inheritdoc/>
		public void UpdateRotation(float delta, Vector3? targetDirection = null, bool ignoreControl = false)
		{
			float time = delta * (ignoreControl ? 1f : rigidbodyWrapper.Control);
			if (!targetDirection.HasValue)
			{
				if (LockRotation && TargetDirection != Vector3.zero)
				{
					// Lock rotation in set target direction.
					Turn(TargetDirection);
				}
				else if (!(rigidbodyWrapper.TargetVelocity == Vector3.zero || rigidbodyWrapper.Velocity.FlattenY() == Vector3.zero))
				{
					// Rotation isn't locked, look in velocity direction when at 100% grip and at target velocity direction when at 0% grip.
					Turn(rigidbodyWrapper.Velocity.FlattenY().normalized.Slerp(
						rigidbodyWrapper.TargetVelocity.FlattenY().normalized,
						rigidbodyWrapper.Grip.InvertClamped().InOutQuint()));
				}
			}
			else if (targetDirection.Value != Vector3.zero)
			{
				// Turn towards target direction.
				Turn(targetDirection.Value);
			}

			void Turn(Vector3 dir, float speed = 1f)
			{
				rigidbodyWrapper.Rotation = Quaternion.Slerp(
					rigidbodyWrapper.Rotation,
					Quaternion.LookRotation(dir),
					speed * rotationSmoothing * time);
			}
		}

		/// <inheritdoc/>
		public void ForceRotation(Vector3? direction = null)
		{
			if (!direction.HasValue && rigidbodyWrapper.TargetVelocity == Vector3.zero ||
				direction.HasValue && direction.Value == Vector3.zero)
			{
				return;
			}

			Entity.GameObject.transform.rotation = direction.HasValue ?
				Quaternion.LookRotation(direction.Value, Vector3.up) :
				Quaternion.LookRotation(rigidbodyWrapper.TargetVelocity.FlattenY(), Vector3.up);
		}

		/// <inheritdoc/>
		public float CalculateSpeed(float input)
		{
			return input < inputSettings.MinimumInput ? MinSpeed * (input / inputSettings.MinimumInput) :
				input < 0.5f ? MinSpeed.Lerp(HalfSpeed, input * 2f) :
				input < 1f ? HalfSpeed.Lerp(FullSpeed, (input - 0.5f) * 2f) :
				FullSpeed * input;
		}
	}
}
