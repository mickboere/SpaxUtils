using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	[DefaultExecutionOrder(30)]
	public class GroundedMovementHandler : EntityComponentBase, IAgentMovementHandler
	{
		/// <inheritdoc/>
		public Vector3 InputAxis
		{
			get { return _inputAxis; }
			set
			{
				_inputAxis = value.FlattenY();
			}
		}
		private Vector3 _inputAxis;

		/// <inheritdoc/>
		public Vector3 MovementInput
		{
			get { return _movementInput; }
			set
			{
				_movementInput = value;
				rigidbodyWrapper.TargetVelocity = value == Vector3.zero ? Vector3.zero : (Quaternion.LookRotation(InputAxis) * value * speed);
			}
		}
		private Vector3 _movementInput;

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
		public float MovementSpeed { get { return speed; } set { speed = value; } }

		/// <inheritdoc/>
		public bool LockRotation { get; set; }

		/// <inheritdoc/>
		public float RotationSpeed { get; set; }

		[SerializeField] private float speed = 4.5f;
		[SerializeField] private float rotationSmoothingSpeed = 30f;
		[SerializeField] private float controlForce = 1800f;
		[SerializeField] private float brakeForce = 900f;
		[SerializeField] private float power = 40f;

		private RigidbodyWrapper rigidbodyWrapper;
		private IGrounderComponent grounder;

		public void InjectDependencies(RigidbodyWrapper rigidbodyWrapper, IGrounderComponent grounder)
		{
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.grounder = grounder;
		}

		protected void OnEnable()
		{
			TargetDirection = Transform.forward;
		}

		protected void FixedUpdate()
		{
			UpdateMovement(Time.fixedDeltaTime);
			UpdateRotation(Time.fixedDeltaTime);
		}

		/// <inheritdoc/>
		public void UpdateMovement(float delta, Vector3? targetVelocity = null, bool ignoreControl = false)
		{
			rigidbodyWrapper.ControlAxis = Vector3.one.FlattenY();

			if (!grounder.Sliding)
			{
				rigidbodyWrapper.ApplyMovement(targetVelocity.HasValue ? targetVelocity.Value : rigidbodyWrapper.TargetVelocity,
					controlForce, brakeForce, power, ignoreControl, grounder.Mobility);
			}
			// TODO: Allow for sliding control & overhaul sliding altogether.

			if (!LockRotation)
			{
				TargetDirection = Vector3.Lerp(
					Vector3.Lerp(Transform.forward, rigidbodyWrapper.Velocity, MovementInput.magnitude.Clamp01()),
					rigidbodyWrapper.TargetVelocity,
					rigidbodyWrapper.Grip);
			}
		}

		/// <inheritdoc/>
		public void UpdateRotation(float delta, Vector3? direction = null, bool ignoreControl = false)
		{
			float t = delta * EntityTimeScale * (ignoreControl ? 1f : rigidbodyWrapper.Control);
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
