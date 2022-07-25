using System.Collections.Generic;
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

		protected Rigidbody Rigidbody => wrapper.Rigidbody;

		[SerializeField] private float speed = 2.5f;
		[SerializeField] private float rotationSmoothingSpeed = 20f;
		[SerializeField] private float controlForce = 2000f;
		[SerializeField] private float brakeForce = 200f;
		[SerializeField] private float power = 20f;
		[Header("Slopes")]
		[SerializeField, Range(0f, 90f)] private float maxSurfaceSlope = 45f;
		[SerializeField] private AnimationCurve surfaceTractionCurve = new AnimationCurve(new Keyframe(1f, 1f), new Keyframe(0f, 0f));
		[SerializeField, Range(0f, 90f)] private float maxTerrainSlope = 60f;
		[SerializeField] private AnimationCurve terrainTractionCurve = new AnimationCurve(new Keyframe(1f, 1f), new Keyframe(0f, 0f));

		private RigidbodyWrapper wrapper;
		private IGrounderComponent grounder;

		private Vector3 targetDirection;
		private Vector3 inputAxis;

		public void InjectDependencies(RigidbodyWrapper wrapper, IGrounderComponent grounder)
		{
			this.wrapper = wrapper;
			this.grounder = grounder;
		}

		protected void OnEnable()
		{
			targetDirection = Transform.forward;
		}

		protected void FixedUpdate()
		{
			ApplyMovement();
		}

		protected void Update()
		{
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
			SetTargetVelocity(Quaternion.LookRotation(inputAxis) * input * speed);
		}

		/// <inheritdoc/>
		public void SetTargetVelocity(Vector3 velocity)
		{
			wrapper.TargetVelocity = velocity;
		}

		/// <inheritdoc/>
		public void SetInputAxis(Vector3 axis)
		{
			inputAxis = axis.FlattenY().normalized;
		}

		/// <inheritdoc/>
		public void SetTargetDirection(Vector3 direction)
		{
			targetDirection = direction.FlattenY().normalized;
		}

		/// <inheritdoc/>
		public void ForceRotation()
		{
			if (wrapper.TargetVelocity == Vector3.zero)
			{
				return;
			}

			Entity.GameObject.transform.rotation = Quaternion.LookRotation(wrapper.TargetVelocity);
		}

		private void ApplyMovement()
		{
			wrapper.ControlAxis = Vector3.one.FlattenY();

			float surfaceAngle = grounder.SurfaceTraction.Invert() * 90f;
			float surfaceLoss = surfaceAngle > maxSurfaceSlope ? 0f : surfaceAngle / maxSurfaceSlope;
			float terrainAngle = grounder.TerrainTraction.Invert() * 90f;
			float terrainLoss = terrainAngle > maxTerrainSlope ? 0f : terrainAngle / maxTerrainSlope;
			float traction = surfaceLoss > terrainLoss ? surfaceTractionCurve.Evaluate(surfaceLoss) : terrainTractionCurve.Evaluate(terrainLoss);

			//SpaxDebug.Log($"{traction}", $"surfAngle={surfaceAngle}, surfTrac={surfaceLoss}, terAngle={terrainAngle}, terTrac={terrainLoss}");
			wrapper.ApplyMovement(controlForce, brakeForce, power, false, traction);

			SetTargetDirection(Vector3.Lerp(Vector3.Lerp(Transform.forward, wrapper.Velocity, wrapper.Control),
				wrapper.TargetVelocity, wrapper.Grip));
		}

		private void ApplyRotation()
		{
			if (targetDirection == Vector3.zero)
			{
				if (wrapper.Velocity.FlattenY() != Vector3.zero)
				{
					Rigidbody.rotation = Quaternion.Lerp(Rigidbody.rotation, Quaternion.LookRotation(wrapper.Velocity.FlattenY()),
						rotationSmoothingSpeed * Time.deltaTime * wrapper.Control);
				}
				return;
			}

			Rigidbody.rotation = Quaternion.Lerp(Rigidbody.rotation, Quaternion.LookRotation(targetDirection),
				rotationSmoothingSpeed * Time.deltaTime * wrapper.Control);
		}
	}
}
