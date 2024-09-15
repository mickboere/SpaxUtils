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
				_inputAxis = value.FlattenY().normalized;
			}
		}
		private Vector3 _inputAxis = Vector3.forward;

		/// <inheritdoc/>
		public Vector3 MovementInputRaw { get; set; }

		/// <inheritdoc/>
		public Vector3 MovementInputSmooth { get; private set; }

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
		public float MovementSpeed { get { return speed * moveSpeedStat; } set { speed = value; } }

		/// <inheritdoc/>
		public bool LockRotation { get; set; }

		[SerializeField] private float speed = 4.5f;
		[SerializeField] private float rotationSmoothingSpeed = 30f;
		[SerializeField] private float controlForce = 1800f;
		[SerializeField] private float brakeForce = 900f;
		[SerializeField] private float power = 40f;
		[SerializeField] private StatCost sprintCost;
		[SerializeField] private float tiredInputLimiter = 0.666f;

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

			moveSpeedStat = Agent.GetStat(AgentStatIdentifiers.MOVEMENT_SPEED, true, 1f);
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
			Vector3 input = Entity.TryGetStat(sprintCost.Stat, out EntityStat cost) ?
					(cost > 0f || MovementInputRaw == Vector3.zero ? MovementInputRaw : MovementInputRaw.ClampMagnitude(tiredInputLimiter)) :
					MovementInputRaw;
			// Update smooth input value.
			MovementInputSmooth = inputHelper.Update(input, delta);

			if (!targetVelocity.HasValue)
			{
				// Calculate target velocity from current input.
				rigidbodyWrapper.TargetVelocity = MovementInputRaw == Vector3.zero ? Vector3.zero : Quaternion.LookRotation(InputAxis) * MovementInputSmooth * MovementSpeed;
			}

			if (!grounder.Sliding)
			{
				rigidbodyWrapper.ApplyMovement(targetVelocity, controlForce, brakeForce, power, ignoreControl, grounder.Mobility);

				if (MovementInputRaw.magnitude > 1.01f)
				{
					Entity.TryApplyStatCost(sprintCost, rigidbodyWrapper.Speed * rigidbodyWrapper.Mass * (MovementInputRaw.magnitude - 1f) * delta, out bool drained);
				}
			}
			//else
			//{
			// TODO: Allow for sliding control & overhaul sliding altogether.
			//}

			if (!LockRotation)
			{
				TargetDirection = Vector3.Lerp(
					Vector3.Lerp(Transform.forward, rigidbodyWrapper.Velocity, MovementInputRaw.magnitude.Clamp01()),
					rigidbodyWrapper.TargetVelocity,
					rigidbodyWrapper.Grip);
			}
		}

		/// <inheritdoc/>
		public void UpdateRotation(float delta, Vector3? direction = null, bool ignoreControl = false)
		{
			float t = delta * (ignoreControl ? 1f : rigidbodyWrapper.Control);
			Vector3 d = direction.HasValue ? direction.Value == Vector3.zero ? TargetDirection : direction.Value : TargetDirection;

			if (d == Vector3.zero)
			{
				if (rigidbodyWrapper.Velocity.FlattenY() != Vector3.zero)
				{
					rigidbodyWrapper.Rotation = Quaternion.Lerp(
						rigidbodyWrapper.Rotation,
						Quaternion.LookRotation(rigidbodyWrapper.Velocity.FlattenY()),
						rotationSmoothingSpeed * t);
				}
			}
			else
			{
				rigidbodyWrapper.Rotation = Quaternion.Lerp(
					rigidbodyWrapper.Rotation,
					Quaternion.LookRotation(d),
					rotationSmoothingSpeed * t);
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
				Quaternion.LookRotation(direction.Value, Entity.GameObject.transform.up) :
				Quaternion.LookRotation(rigidbodyWrapper.TargetVelocity.FlattenY());
		}
	}
}
