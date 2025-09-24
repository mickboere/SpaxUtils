using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Surveyor wheel implementation that simulates foot movement of an entity to dictate more accurate movement animation.
	/// </summary>
	public class SurveyorComponent : EntityComponentMono
	{
		public float Effect { get; private set; }
		public float Stride { get; private set; }
		public float Circumference { get; private set; }

		[Header("Surveyor")]
		[SerializeField] private float strideLength = 1f;
		[SerializeField] private float minStride = 0.3f;
		[SerializeField] private float maxStride = 1.3f;
		[SerializeField] private AnimationCurve strideSpeed = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[SerializeField, MinMaxRange(0f, 1f, true)] private Vector2 groundedRange = new Vector2(0.35f, 0.65f);
		[SerializeField, Range(0f, 1f)] private float groundThreshold = 0.5f;
		[SerializeField] private float liftThreshold = 0.025f;
		[SerializeField, Range(1f, 2f)] private float surveyLength = 1.3f;
		[SerializeField] private float maxReach = 1.5f;
		[SerializeField] private Vector3 surveyOriginOffset;
		[SerializeField, Range(0f, 1f)] private float surveyOriginCOMInfluence = 0f;
		[SerializeField, Range(0f, 2f)] private float surveyOriginVelocityInfluence = 1.25f;
		[SerializeField] private float maxFootingAngle = 75f;
		[Header("Stride Influence")]
		[SerializeField, Range(0f, 1f)] private float mobilityStrideInfluence = 1f;
		[SerializeField, Range(0f, 1f)] private float accelerationStrideInfluence = 1f;
		[SerializeField] private Vector2 accelerationSmoothing = new Vector2(10f, 2f);
		[Header("Ground check")]
		[SerializeField] private LayerMask layerMask;
		[SerializeField, Tooltip("Grounded amount calculated from raycast approaching the ground.")] private AnimationCurve anticipationCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[SerializeField, Tooltip("Grounded amount calculated from raycast exiting the ground.")] private AnimationCurve exitCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[SerializeField, Range(0f, 1f)] private float groundedOvershoot = 0f;
		[SerializeField] private float checkRadius = 0.1f;
		[SerializeField] private float clipThreshold = 0.025f;

		[SerializeField, Header("Gizmos")] private bool debug;
		[SerializeField, Conditional(nameof(debug))] private bool drawSpokes;
		[SerializeField, Conditional(nameof(debug))] private bool drawRaycasts;

		private float progress;
		private RigidbodyWrapper rigidbodyWrapper;
		private AgentLegsComponent legs;
		private IAgentBody body;
		private IGrounderComponent grounder;
		private IAgentMovementHandler movementHandler;

		private float smoothAccel;
		private float previousAccel;

		public void InjectDependencies(RigidbodyWrapper wrapper, AgentLegsComponent legs, IAgentBody body,
			IGrounderComponent grounder, IAgentMovementHandler movementHandler)
		{
			this.rigidbodyWrapper = wrapper;
			this.legs = legs;
			this.grounder = grounder;
			this.body = body;
			this.movementHandler = movementHandler;
		}

		public void ResetSurveyor()
		{
			progress = 0f;
			foreach (Leg leg in legs.Legs)
			{
				leg.UpdateFoot(false, 0f, false, default, default);
			}
		}

		public void UpdateSurveyor(float delta)
		{
			UpdateSurveyor(delta * EntityTimeScale, rigidbodyWrapper.Velocity.FlattenY().magnitude);
			UpdateLegs(delta * EntityTimeScale);
		}

		public void UpdateSurveyor(float delta, float velocity)
		{
			if (!grounder.Grounded || grounder.Sliding || velocity < 0.01f)
			{
				return;
			}

			Effect = velocity / movementHandler.FullSpeed;

			// Mobility Influence
			Effect *= Mathf.Lerp(1f, grounder.Mobility, mobilityStrideInfluence);

			// Acceleration Influence
			float accel = rigidbodyWrapper.Acceleration.magnitude;
			smoothAccel = Mathf.Lerp(smoothAccel, 1f - accel, (accel > previousAccel ? accelerationSmoothing.x : accelerationSmoothing.y) * delta);
			previousAccel = accel;
			Effect *= Mathf.Lerp(1f, smoothAccel, accelerationStrideInfluence);

			Stride = Mathf.Clamp(strideLength * Effect, minStride, maxStride);
			Circumference = Stride * Mathf.PI;

			float travel = velocity / Circumference;
			progress = (progress + travel * delta).Repeat();
		}

		public float GetProgress(float offset = 0f, bool applyStrideSpeedCurve = true)
		{
			float p = (progress + offset).Repeat();
			return applyStrideSpeedCurve ? strideSpeed.Evaluate(p) : p;
		}

		public float CalculateFootHeight(float progress)
		{
			return 1f + Mathf.Sin(-progress.Repeat() * Mathf.PI);
		}

		/// <summary>
		/// Returns a Vector3 spoke direction.
		/// </summary>
		/// <param name="progress">The progress into the surveyor cycle; 0 being forwards and 1 being backwards.</param>
		/// <param name="origin">The ideal origin position to cast the surveyor spoke from.</param>
		/// <returns>Vector3 spoke direction</returns>
		public Vector3 GetSurveyorSpoke(float progress, out Vector3 origin)
		{
			float z = Mathf.Cos(progress * Mathf.PI);
			float y = Mathf.Sin(-progress * Mathf.PI);
			Vector3 dir = (rigidbodyWrapper.TargetVelocity == Vector3.zero ? rigidbodyWrapper.Rotation : Quaternion.LookRotation(rigidbodyWrapper.TargetVelocity)) * (new Vector3(0f, y, z).normalized * Stride);
			origin = rigidbodyWrapper.Position + rigidbodyWrapper.Rotation * (surveyOriginOffset + (Vector3.up * Stride));
			return dir;
		}

		private void UpdateLegs(float delta)
		{
			if (rigidbodyWrapper.Control < 0.5f || grounder.Sliding)
			{
				foreach (Leg leg in legs.Legs)
				{
					Vector3 dir = leg.SolePos - leg.KneePos;
					float length = dir.magnitude + clipThreshold * 2f;
					if (Physics.Raycast(leg.KneePos, dir, out RaycastHit hit, length, layerMask) &&
						length - hit.distance > clipThreshold)
					{
						leg.UpdateFoot(grounder.Grounded, 1f, true, hit.point, hit);
					}
					else
					{
						leg.UpdateFoot(grounder.Grounded, 0f, false, leg.TargetPoint, leg.GroundedHit);
					}
				}

				return;
			}

			foreach (Leg leg in legs.Legs)
			{
				float progress = GetProgress(leg.WalkCycleOffset);
				Vector3 direction = GetSurveyorSpoke(progress, out Vector3 origin);

				// Adjust sideways-spacing to always cast above the foot.
				origin += (leg.SolePos - origin).LocalizeDirection(rigidbodyWrapper.transform).FlattenYZ().GlobalizeDirection(rigidbodyWrapper.transform);
				// ^ TODO: Flatten in movement direction instead of Z.
				// Move origin forwards to meet center of mass.
				origin += (body.Center - origin).LocalizeDirection(transform).FlattenXY().GlobalizeDirection(transform) * surveyOriginCOMInfluence;
				// Move origin to match velocity.
				origin += rigidbodyWrapper.Velocity * delta * surveyOriginVelocityInfluence;

				direction *= surveyLength;
				leg.CastOrigin = origin;
				leg.CastDirection = direction;

				float groundedAmount = 0f;
				Vector3 targetPoint = leg.TargetPoint;
				RaycastHit groundedHit = leg.GroundedHit;
				bool validGround = false;
				float lift = leg.Sole.position.LocalizePoint(rigidbodyWrapper.transform).y;
				bool lifted = lift < liftThreshold * body.Scale;
				bool grounded = grounder.Grounded && !lifted;
				float groundRange = progress < groundedRange.x ? progress / groundedRange.x : progress > groundedRange.y ? progress.InverseLerp(groundedRange.y, 1f) : 1f;

				// Cast to find valid target foot position.
				if (Physics.SphereCast(origin, checkRadius, direction, out RaycastHit hit, Stride * surveyLength, layerMask))
				{
					// Find safe spot if hit normal exceeds max footing angle.
					if (hit.normal.Dot(rigidbodyWrapper.Up).InvertClamped() * 90f > maxFootingAngle)
					{
						if (TryFindSafeSpot(hit.point, out RaycastHit safeSpot))
						{
							groundedHit = safeSpot;
							validGround = true;
						}
					}
					else
					{
						groundedHit = hit;
						validGround = true;
					}
				}

				// If on valid ground, determine grounding values.
				if (validGround)
				{
					float hitDistance = Vector3.Distance(origin, hit.point) - Stride;
					float fade = Mathf.Clamp01(hitDistance / (Stride * surveyLength - Stride)).Invert().Min(groundRange);
					groundedAmount = progress < groundThreshold ? anticipationCurve.Evaluate(fade) : exitCurve.Evaluate(fade);

					// Only allow changing target point if foot is not grounded.
					if (progress < groundThreshold && (!lifted || groundedAmount < 0.99f))
					{
						targetPoint = hit.point;
					}
				}

				if (targetPoint.Distance(leg.ThighPos) > leg.Length * maxReach)
				{
					groundedAmount = 0f;
				}

				leg.UpdateFoot(grounded, groundedAmount, validGround, targetPoint, groundedHit);
			}
		}

		/// <summary>
		/// Starting at <paramref name="end"/>, cast <paramref name="iterations"/> rays until current RB position is reached to find a safe spot to place feet.
		/// </summary>
		private bool TryFindSafeSpot(Vector3 end, out RaycastHit safeSpot, int iterations = 6)
		{
			for (int i = 1; i <= iterations; i++)
			{
				float f = (float)i / iterations;
				Vector3 t = Vector3.Lerp(end, rigidbodyWrapper.Position, f);
				Vector3 origin = t + rigidbodyWrapper.Up * body.Scale;

				if (Physics.Raycast(origin, -rigidbodyWrapper.Up, out RaycastHit hit, surveyLength * body.Scale, layerMask) &&
					hit.normal.Dot(rigidbodyWrapper.Up).InvertClamped() * 90f < maxFootingAngle)
				{
					safeSpot = hit;
					if (debug)
					{
						Debug.DrawLine(origin, hit.point, Color.green, 1f);
					}
					return true;
				}
				else if (debug)
				{
					Debug.DrawRay(origin, -rigidbodyWrapper.Up * surveyLength * body.Scale, Color.red, 1f);
				}
			}

			safeSpot = default;
			return false;
		}

		protected void OnDrawGizmos()
		{
			if (!debug)
			{
				return;
			}

			if (drawSpokes)
			{
				Gizmos.color = Color.red;
				DrawSpoke(0f);
				Gizmos.color = Color.yellow;
				DrawSpoke(0.5f);
			}

			if (drawRaycasts && legs != null)
			{
				for (int i = 0; i < legs.Legs.Count; i++)
				{
					Leg leg = legs.Legs[i];
					Vector3 target = leg.CastOrigin + leg.CastDirection.normalized * Stride;
					Vector3 survey = leg.CastDirection.normalized * (surveyLength - 1f);

					Gizmos.color = new Color(leg.GroundedAmount, leg.GroundedAmount, leg.GroundedAmount);
					Gizmos.DrawRay(leg.CastOrigin, leg.CastDirection.normalized * Stride);
					Gizmos.color = GetProgress(leg.WalkCycleOffset, false) < groundThreshold ? Color.yellow : Color.magenta;
					Gizmos.DrawSphere(target, 0.03f);
					Gizmos.DrawSphere(target + survey, 0.03f);

					if (!leg.ValidGround)
					{
						Gizmos.DrawRay(target, survey);
					}
					else
					{
						Gizmos.color = Color.red;
						Gizmos.DrawLine(leg.CastOrigin, leg.GroundedHit.point);
						Gizmos.DrawSphere(leg.CastOrigin, 0.03f);
						Gizmos.DrawSphere(leg.GroundedHit.point, 0.03f);
						Gizmos.color = Color.green;
						Gizmos.DrawSphere(leg.TargetPoint, 0.04f);
					}
				}
			}

			void DrawSpoke(float offset)
			{
				Vector3 dir = GetSurveyorSpoke(GetProgress(offset), out Vector3 pos);
				Gizmos.DrawRay(pos, dir);
				Gizmos.DrawSphere(pos, 0.05f);
				Gizmos.DrawSphere(pos + dir, 0.05f);
			}
		}
	}
}
