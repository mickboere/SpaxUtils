﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	[DefaultExecutionOrder(30)]
	public class GroundedMovementHandler : EntityComponentBase, IAgentMovementHandler
	{
		/// <inheritdoc/>
		public Vector3 MovementInput { get; private set; }

		/// <inheritdoc/>
		public float MovementSpeed => speed;

		/// <inheritdoc/>
		public bool LockRotation { get; set; }

		[SerializeField] private float speed = 4.5f;
		[SerializeField] private float rotationSmoothingSpeed = 30f;
		[SerializeField] private float controlForce = 1800f;
		[SerializeField] private float brakeForce = 900f;
		[SerializeField] private float power = 40f;

		private RigidbodyWrapper rigidbodyWrapper;
		private IGrounderComponent grounder;

		private Vector3 targetDirection;
		private Vector3 inputAxis;

		public void InjectDependencies(RigidbodyWrapper rigidbodyWrapper, IGrounderComponent grounder)
		{
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.grounder = grounder;
		}

		protected void OnEnable()
		{
			SetTargetDirection(Transform.forward);
		}

		protected void FixedUpdate()
		{
			ApplyMovement();
			ApplyRotation();
		}

		/// <inheritdoc/>
		public void SetMovementSpeed(float speed)
		{
			this.speed = speed;
		}

		/// <inheritdoc/>
		public void SetRotationSpeed(float speed)
		{
			this.speed = speed;
		}

		/// <inheritdoc/>
		public void SetMovementInput(Vector2 input)
		{
			SetMovementInput(new Vector3(input.x, 0f, input.y));
		}

		/// <inheritdoc/>
		public void SetMovementInput(Vector3 input)
		{
			MovementInput = input;
			SetTargetVelocity(input == Vector3.zero ? Vector3.zero : (Quaternion.LookRotation(inputAxis) * input * speed));
		}

		/// <inheritdoc/>
		public void SetTargetVelocity(Vector3 velocity)
		{
			rigidbodyWrapper.TargetVelocity = velocity;
		}

		/// <inheritdoc/>
		public void SetInputAxis(Vector3 axis)
		{
			inputAxis = axis.FlattenY().normalized;
		}

		/// <inheritdoc/>
		public void SetTargetDirection(Vector3 direction)
		{
			targetDirection = direction == Vector3.zero ? rigidbodyWrapper.Forward : direction.FlattenY().normalized;
		}

		/// <inheritdoc/>
		public void ForceRotation(Vector3? direction = null)
		{
			if (!direction.HasValue && rigidbodyWrapper.TargetVelocity == Vector3.zero || direction.HasValue && direction.Value == Vector3.zero)
			{
				return;
			}

			Entity.GameObject.transform.rotation = direction.HasValue ?
				Quaternion.LookRotation(direction.Value, Entity.GameObject.transform.up) :
				Quaternion.LookRotation(rigidbodyWrapper.TargetVelocity.FlattenY());
		}

		private void ApplyMovement()
		{
			rigidbodyWrapper.ControlAxis = Vector3.one.FlattenY();

			if (!grounder.Sliding)
			{
				rigidbodyWrapper.ApplyMovement(controlForce, brakeForce, power, false, grounder.Mobility);
				// TODO: Allow for sliding control & overhaul sliding altogether.
			}

			if (!LockRotation)
			{
				SetTargetDirection(
				Vector3.Lerp(
					Vector3.Lerp(Transform.forward, rigidbodyWrapper.Velocity, MovementInput.magnitude.Clamp01()),
					rigidbodyWrapper.TargetVelocity,
					rigidbodyWrapper.Grip));
			}
		}

		private void ApplyRotation()
		{
			if (targetDirection == Vector3.zero)
			{
				if (rigidbodyWrapper.Velocity.FlattenY() != Vector3.zero)
				{
					rigidbodyWrapper.Rotation = Quaternion.Lerp(rigidbodyWrapper.Rotation, Quaternion.LookRotation(rigidbodyWrapper.Velocity.FlattenY()),
						rotationSmoothingSpeed * Time.fixedDeltaTime * EntityTimeScale * rigidbodyWrapper.Control);
				}
				return;
			}

			rigidbodyWrapper.Rotation = Quaternion.Lerp(rigidbodyWrapper.Rotation, Quaternion.LookRotation(targetDirection),
				rotationSmoothingSpeed * Time.fixedDeltaTime * EntityTimeScale * rigidbodyWrapper.Control);
		}
	}
}
