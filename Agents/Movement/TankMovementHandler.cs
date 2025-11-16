using UnityEngine;

namespace SpaxUtils
{
	[DefaultExecutionOrder(30)]
	public class TankMovementHandler : GroundedMovementHandler
	{
		[Header("Tank Rotation")]
		[SerializeField, Tooltip("Maximum turn speed in degrees per second.")]
		private float tankMaxTurnSpeed = 90f;

		[SerializeField, Tooltip("Angular acceleration for tank-style rotation, in degrees per second squared.")]
		private float tankAngularAcceleration = 360f;

		[SerializeField, Tooltip("If true, only rotates while moving (has velocity or movement input).")]
		private bool onlyTurnWhileMoving = false;

		private float tankAngularVelocity; // signed deg/sec

		/// <inheritdoc/>
		public override void UpdateMovement(float delta, Vector3? targetVelocity = null, bool ignoreControl = false)
		{
			// Grounded movement does not utilize Y axis.
			rigidbodyWrapper.ControlAxis = Vector3.one.FlattenY();

			if (!targetVelocity.HasValue)
			{
				if (InputSmooth == Vector3.zero)
				{
					rigidbodyWrapper.TargetVelocity = Vector3.zero;
				}
				else
				{
					// Snail / tank translation: always move FORWARD when there is input.
					float speed = CalculateSpeed(InputSmooth.magnitude);
					Vector3 forward = (rigidbodyWrapper.Rotation * Vector3.forward).FlattenY().normalized;

					if (forward == Vector3.zero)
					{
						rigidbodyWrapper.TargetVelocity = Vector3.zero;
					}
					else
					{
						rigidbodyWrapper.TargetVelocity = forward * speed;
					}
				}
			}

			if (grounder.Grounded)
			{
				if (!grounder.Sliding)
				{
					// Default movement control (same as base).
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
						// Apply sprint cost (same as base).
						statHandler.PointStats.E.Current.Damage(
							sprintCost * rigidbodyWrapper.Mass *
							(rigidbodyWrapper.Speed / (FullSpeed * 1.5f * moveSpeedStat)) *
							rigidbodyWrapper.Control * delta);
					}
				}
				else if (grounder.SurfaceNormal != Vector3.up)
				{
					// Slope-sliding control (same as base).
					Vector3 right = Vector3.Cross(Vector3.up, grounder.SurfaceNormal);
					Vector3 downhill = right.Cross(grounder.SurfaceNormal);
					Quaternion downQ = Quaternion.LookRotation(downhill, grounder.SurfaceNormal).Inverse();
					float current = (downQ * rigidbodyWrapper.Velocity).x;
					float target = (downQ * (Quaternion.LookRotation(InputAxis) * InputSmooth).ProjectOnPlane(downhill)).x;
					float scale = (rigidbodyWrapper.Velocity.y * slideSpeedSteerDamp).Abs().Clamp01().InOutSine();
					Vector3 force = right * current.CalculateForce(
						target * moveSpeedStat * slideSteeringSpeed * scale,
						power * EntityTimeScale * scale,
						maxAcceleration * EntityTimeScale * scale);
					rigidbodyWrapper.AddForce(force);
				}
			}

			if (debug)
			{
				SpaxDebug.Log($"[{Agent.ID}][Tank]", $"InputRaw={InputRaw}, InputSmooth={InputSmooth}, target={rigidbodyWrapper.TargetVelocity}");
			}
		}

		/// <inheritdoc/>
		public override void UpdateRotation(float delta, Vector3? targetDirection = null, bool ignoreControl = false)
		{
			float deltaTime = delta * (ignoreControl ? 1f : rigidbodyWrapper.Control);

			// Movement check for "only turn while moving".
			Vector3 flatVelocity = rigidbodyWrapper.Velocity.FlattenY();
			bool isMoving =
				flatVelocity.sqrMagnitude > 0.0001f ||
				InputSmooth.sqrMagnitude > 0.0001f;

			if (onlyTurnWhileMoving && !isMoving)
			{
				return;
			}

			if (!targetDirection.HasValue)
			{
				if (grounder.Sliding)
				{
					// Turn towards velocity direction.
					Turn(flatVelocity.normalized, 1f);
				}
				else if (LockRotation && TargetDirection != Vector3.zero)
				{
					// Lock rotation in set target direction.
					Turn(TargetDirection, 1f);
				}
				else
				{
					// Tank/snail: rotate towards desired input direction.
					if (InputSmooth != Vector3.zero)
					{
						// Desired direction in world space from camera/enemy framing.
						Vector3 desiredDir = (Quaternion.LookRotation(InputAxis) * InputSmooth).FlattenY().normalized;

						if (desiredDir != Vector3.zero)
						{
							Turn(desiredDir, 1f);
						}
					}
				}
			}
			else if (targetDirection.Value != Vector3.zero)
			{
				// Turn towards explicit target direction.
				Turn(targetDirection.Value, 1f);
			}

			void Turn(Vector3 dir, float speed)
			{
				if (dir == Vector3.zero)
				{
					return;
				}

				Quaternion targetRot = Quaternion.LookRotation(dir);

				// First, use the same smoothing model as base to get a "soft" target.
				Quaternion smoothedTarget = rigidbodyWrapper.Rotation.FISlerp(
					targetRot,
					speed * rotationSmoothing * deltaTime);

				// Work purely in yaw (XZ plane).
				Vector3 currentForward = (rigidbodyWrapper.Rotation * Vector3.forward).FlattenY().normalized;
				Vector3 desiredForward = (smoothedTarget * Vector3.forward).FlattenY().normalized;

				if (currentForward == Vector3.zero || desiredForward == Vector3.zero)
				{
					return;
				}

				// Signed yaw difference in degrees.
				float signedAngle = Vector3.SignedAngle(currentForward, desiredForward, Vector3.up);

				// Close enough: snap + stop rotation to avoid jitter.
				if (Mathf.Abs(signedAngle) < 0.1f)
				{
					tankAngularVelocity = 0f;
					rigidbodyWrapper.Rotation = smoothedTarget;
					return;
				}

				// Desired angular speed: max tank speed.
				float desiredAngularSpeed = Mathf.Sign(signedAngle) * tankMaxTurnSpeed * speed;

				// Inertia: accelerate angular velocity towards the desired speed.
				tankAngularVelocity = Mathf.MoveTowards(
					tankAngularVelocity,
					desiredAngularSpeed,
					tankAngularAcceleration * deltaTime);

				// Max angle we can rotate this frame given current angular velocity.
				float maxStep = Mathf.Abs(tankAngularVelocity) * deltaTime;
				if (maxStep <= 0f)
				{
					return;
				}

				// Do not overshoot the remaining angle.
				float step = Mathf.Min(Mathf.Abs(signedAngle), maxStep);
				float stepSigned = Mathf.Sign(signedAngle) * step;

				// Apply yaw around up axis.
				Quaternion deltaRot = Quaternion.AngleAxis(stepSigned, Vector3.up);
				rigidbodyWrapper.Rotation = deltaRot * rigidbodyWrapper.Rotation;
			}
		}
	}

}
