using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Surveyor wheel implementation that simulates foot movement of an entity to dictate more accurate movement animation.
	/// </summary>
	/// https://github.com/mickboere/SpaxUtils/blob/master/Animation/Procedural/LegWalkerComponent.cs
	public class LegWalkerComponent : EntityComponentBase
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
		[SerializeField] private AnimationCurve surfaceTractionStride = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[SerializeField] private AnimationCurve terrainTractionStride = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[SerializeField, MinMaxRange(0f, 1f, true)] private Vector2 groundedRange = new Vector2(0.4f, 0.6f);
		[SerializeField] private float surveyLength = 1.3f;
		[SerializeField] private float maxReach = 1.5f;
		[SerializeField] private Vector3 surveyOriginOffset;
		[SerializeField, Range(0f, 1f)] private float surveyOriginCOMInfluence = 0.5f;
		[SerializeField, Range(0f, 2f)] private float surveyOriginVelocityInfluence = 0.5f;
		[Header("Ground check")]
		[SerializeField] private LayerMask layerMask;
		[SerializeField, Tooltip("Grounded amount calculated from raycast approaching the ground.")] private AnimationCurve anticipationCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[SerializeField, Tooltip("Grounded amount calculated from raycast exiting the ground.")] private AnimationCurve exitCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[SerializeField, Range(0f, 1f)] private float groundedOvershoot = 0f;
		[SerializeField] private float checkRadius = 0.1f;
		[SerializeField] private float groundedAmountSmoothing = 16f;

		[SerializeField, Header("Gizmos")] private bool drawGizmos;
		[SerializeField, Conditional(nameof(drawGizmos))] private bool drawSpokes;
		[SerializeField, Conditional(nameof(drawGizmos))] private bool drawRaycasts;

		private float progress;
		private RigidbodyWrapper wrapper;
		private ILegsComponent legs;
		private IGrounderComponent grounder;
		private IAgentBody body;

		public void InjectDependencies(RigidbodyWrapper wrapper, ILegsComponent legs, IGrounderComponent grounder, IAgentBody body)
		{
			this.wrapper = wrapper;
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
			UpdateSurveyor(delta, wrapper.HorizontalSpeed);
			UpdateLegs(delta);
		}

		public void UpdateSurveyor(float delta, float velocity)
		{
			if (velocity < 0.01f)
			{
				return;
			}

			Effect = velocity / defaultSpeed;
			Stride = Mathf.Clamp(strideLength * Effect, minStride, maxStride) * Mathf.Min(surfaceTractionStride.Evaluate(grounder.SurfaceTraction), terrainTractionStride.Evaluate(grounder.TerrainTraction));
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
			if (!wrapper.HasControl)
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
				origin += wrapper.Velocity * delta * surveyOriginVelocityInfluence;

				direction *= surveyLength;
				leg.CastOrigin = origin;
				leg.CastDirection = direction;

				float groundedAmount = 0f;
				Vector3 targetPoint = leg.TargetPoint;
				RaycastHit groundedHit = leg.GroundedHit;
				bool validGround = false;
				if (Physics.SphereCast(origin, checkRadius, direction, out RaycastHit hit, Stride * surveyLength, layerMask))
				{
					validGround = true;
					groundedHit = hit;
					if (progress < 0.5f)
					{
						targetPoint = hit.point;
					}

					float fade = Mathf.Clamp01(Mathf.InverseLerp(Stride * surveyLength, Stride, hit.distance));
					groundedAmount = progress < 0.5f ? anticipationCurve.Evaluate(fade * 2f) : exitCurve.Evaluate((fade - 0.5f) * 2f);
				}

				if (targetPoint.Distance(leg.Thigh.position) > leg.Length * maxReach)
				{
					groundedAmount = 0f;
				}

				bool grounded = progress > groundedRange.x && progress < groundedRange.y;
				groundedAmount = Mathf.Lerp(leg.GroundedAmount, groundedAmount, groundedAmountSmoothing * delta);
				leg.UpdateFoot(grounded, groundedAmount, validGround, targetPoint, groundedHit);
			}
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
					}
					else
					{
						Gizmos.color = GetProgress(leg.WalkCycleOffset, false) < 0.5f ? Color.yellow : Color.magenta;
						Gizmos.DrawRay(leg.CastOrigin + leg.CastDirection.normalized * Stride, leg.CastDirection.normalized * (surveyLength - 1f));
					}
				}
			}

			void DrawSpoke(float offset)
			{
				Vector3 dir = GetSurveyorSpoke(GetProgress(offset), out Vector3 pos);
				Gizmos.DrawRay(pos, dir);
			}
		}
	}
}
