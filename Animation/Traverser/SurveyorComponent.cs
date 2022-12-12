using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Surveyor wheel implementation that simulates foot movement of an entity to dictate more accurate movement animation.
	/// </summary>
	/// https://github.com/mickboere/SpaxUtils/blob/master/Animation/Procedural/LegWalkerComponent.cs
	public class SurveyorComponent : EntityComponentBase
	{
		public float Effect { get; private set; }
		public float Stride { get; private set; }
		public float Circumference { get; private set; }

		[Header("Surveyor")]
		[SerializeField] private float defaultSpeed = 4f;
		[SerializeField] private float strideLength = 1f;
		[SerializeField] private float minStride = 0.3f;
		[SerializeField] private float maxStride = 1.3f;
		[SerializeField] private AnimationCurve strideSpeed = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[SerializeField, MinMaxRange(0f, 1f, true)] private Vector2 groundedRange = new Vector2(0.4f, 0.6f);
		[SerializeField] private float surveyLength = 1.3f;
		[SerializeField] private float maxReach = 1.5f;
		[SerializeField] private Vector3 surveyOriginOffset;
		[SerializeField, Range(0f, 1f)] private float surveyOriginCOMInfluence = 0.5f;
		[SerializeField, Range(0f, 2f)] private float surveyOriginVelocityInfluence = 0.5f;
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

		[SerializeField, Header("Gizmos")] private bool drawGizmos;
		[SerializeField, Conditional(nameof(drawGizmos))] private bool drawSpokes;
		[SerializeField, Conditional(nameof(drawGizmos))] private bool drawRaycasts;

		private float progress;
		private RigidbodyWrapper rigidbodyWrapper;
		private ILegsComponent legs;
		private IGrounderComponent grounder;
		private IAgentBody body;

		private float smoothAccel;
		private float previousAccel;

		public void InjectDependencies(RigidbodyWrapper wrapper, ILegsComponent legs, IGrounderComponent grounder, IAgentBody body)
		{
			this.rigidbodyWrapper = wrapper;
			this.legs = legs;
			this.grounder = grounder;
			this.body = body;
		}

		public void ResetSurveyor()
		{
			progress = 0f;
			foreach (ILeg leg in legs.Legs)
			{
				leg.UpdateFoot(false, 0f, false, default, default);
			}
		}

		public void UpdateSurveyor(float delta)
		{
			UpdateSurveyor(delta * EntityTimeScale, rigidbodyWrapper.HorizontalSpeed);
			UpdateLegs(delta * EntityTimeScale);
		}

		public void UpdateSurveyor(float delta, float velocity)
		{
			if (!grounder.Grounded || grounder.Sliding || velocity < 0.01f)
			{
				return;
			}

			Effect = velocity / defaultSpeed;

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
			Vector3 dir = transform.rotation * (new Vector3(0f, y, z).normalized * Stride);
			origin = transform.position + transform.rotation * (surveyOriginOffset + (Vector3.up * Stride));
			return dir;
		}

		private void UpdateLegs(float delta)
		{
			if (rigidbodyWrapper.Control < 0.5f)
			{
				return;
			}

			foreach (ILeg leg in legs.Legs)
			{
				float progress = GetProgress(leg.WalkCycleOffset);
				Vector3 direction = GetSurveyorSpoke(progress, out Vector3 origin);

				// Move origin sideways to match thigh.
				origin += (leg.Thigh.position - origin).Localize(transform).FlattenYZ().Globalize(transform);

				// Move origin forwards to meet center of mass.
				origin += (body.Center - origin).Localize(transform).FlattenXY().Globalize(transform) * surveyOriginCOMInfluence;

				// Move origin to match velocity.
				origin += rigidbodyWrapper.Velocity * delta * surveyOriginVelocityInfluence;

				direction *= surveyLength;
				leg.CastOrigin = origin;
				leg.CastDirection = direction;

				float groundedAmount = 0f;
				Vector3 targetPoint = leg.TargetPoint;
				RaycastHit groundedHit = leg.GroundedHit;
				bool validGround = false;

				// Cast to find valid target foot position.
				if (Physics.SphereCast(origin, checkRadius, direction, out RaycastHit hit, Stride * surveyLength, layerMask))
				{
					// Find safe spot if hit normal exceeds max footing angle.
					if (hit.normal.Dot(rigidbodyWrapper.Up).InvertClamped() * 90f > maxFootingAngle && TryFindSafeSpot(hit.point, out RaycastHit safeSpot))
					{
						hit = safeSpot;
					}

					validGround = true;
					groundedHit = hit;
					if (progress < 0.5f)
					{
						targetPoint = hit.point;
					}

					float hitDistance = Vector3.Distance(origin, hit.point) - Stride;
					float fade = Mathf.Clamp01(hitDistance / (Stride * surveyLength - Stride)).Invert();
					groundedAmount = progress < 0.5f ? anticipationCurve.Evaluate(fade) : exitCurve.Evaluate(fade);
				}

				if (targetPoint.Distance(leg.Thigh.position) > leg.Length * maxReach)
				{
					groundedAmount = 0f;
				}

				bool grounded = grounder.Grounded && progress > groundedRange.x && progress < groundedRange.y;
				leg.UpdateFoot(grounded, groundedAmount, validGround, targetPoint, groundedHit);
			}
		}

		private bool TryFindSafeSpot(Vector3 end, out RaycastHit safeSpot, int iterations = 6)
		{
			for (int i = 1; i <= iterations; i++)
			{
				float f = (float)i / iterations;
				Vector3 t = Vector3.Lerp(end, rigidbodyWrapper.Position, f);
				Vector3 origin = t + rigidbodyWrapper.Up * body.Scale;
				//if (Physics.SphereCast(origin, checkRadius, -rigidbodyWrapper.Up, out RaycastHit hit, surveyLength * body.Scale, layerMask))
				//{
				//	safeSpot = hit;
				//	return true;
				//}

				Debug.DrawRay(origin, -rigidbodyWrapper.Up * surveyLength * body.Scale, Color.red, 1f);

				if (Physics.Raycast(origin, -rigidbodyWrapper.Up, out RaycastHit hit, surveyLength * body.Scale, layerMask))
				{
					safeSpot = hit;
					return true;
				}
			}

			safeSpot = default;
			return false;
		}

		protected void OnDrawGizmos()
		{
			if (!drawGizmos)
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
					ILeg leg = legs.Legs[i];

					Gizmos.color = new Color(0f, 0.2f, 0.9f);
					Gizmos.DrawRay(leg.CastOrigin, leg.CastDirection.normalized * Stride);

					if (leg.ValidGround)
					{
						Gizmos.color = Color.red;
						Gizmos.DrawLine(leg.CastOrigin, leg.GroundedHit.point);
						Gizmos.DrawSphere(leg.CastOrigin, 0.05f);
						Gizmos.DrawSphere(leg.CastOrigin + leg.GroundedHit.point, 0.05f);
					}
					else
					{
						Gizmos.color = GetProgress(leg.WalkCycleOffset, false) < 0.5f ? Color.yellow : Color.magenta;
						Vector3 pos = leg.CastOrigin + leg.CastDirection.normalized * Stride;
						Vector3 dir = leg.CastDirection.normalized * (surveyLength - 1f);
						Gizmos.DrawRay(pos, dir);
						Gizmos.DrawSphere(pos, 0.05f);
						Gizmos.DrawSphere(pos + dir, 0.05f);
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
