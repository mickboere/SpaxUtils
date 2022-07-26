using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	[DefaultExecutionOrder(-200)]
	public class GrounderComponent : EntityComponentBase, IGrounderComponent
	{
		/// <inheritdoc/>
		public bool Grounded { get; private set; }

		/// <inheritdoc/>
		public float Traction => Mathf.Min(SurfaceTraction, TerrainTraction);

		/// <inheritdoc/>
		public float SurfaceTraction { get; private set; }

		/// <inheritdoc/>
		public float TerrainTraction { get; private set; }

		public Vector3 SurfaceNormal { get; private set; }
		public Vector3 TerrainNormal { get; private set; }
		public Vector3 StepPoint { get; private set; }

		[SerializeField] private LayerMask layerMask;
		[SerializeField] private float gravity = 9f;
		[Header("Ground")]
		[SerializeField] private float groundOffset = 1f;
		[SerializeField] private float groundReach = 0.25f;
		[SerializeField] private float groundRadius = 0.2f;
		[Header("Step")]
		[SerializeField] private float stepHeight = 1f;
		[SerializeField] private float stepRadius = 0.35f;
		[SerializeField, Range(0f, 20f)] private float stepSmooth = 20f;
		[SerializeField] private int rayCount = 8;
		[Header("Traction")]
		[SerializeField, Range(0f, 20f)] private float normalSmoothing = 10f;
		[SerializeField, Range(0f, 5f)] private float surfaceGrip = 1f;
		[SerializeField, Range(0f, 90f)] private float maxSurfaceAngle = 90f;
		[SerializeField, Range(0f, 90f)] private float maxTerrainAngle = 90f;
		[SerializeField] private AnimationCurve surfaceTractionCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[Header("Debug")]
		[SerializeField] private bool debug;
		[SerializeField, Conditional(nameof(debug))] private float debugSize = 0.25f;

		private RigidbodyWrapper wrapper;

		private RaycastHit groundedHit;
		private Vector3[] stepPoints;
		private Vector3[] stepNormals;

		public void InjectDependencies(RigidbodyWrapper wrapper)
		{
			this.wrapper = wrapper;
		}

		protected void FixedUpdate()
		{
			GroundCheck();
			StepCheck();
			SurfaceTraction = CalculateTraction(SurfaceNormal, maxSurfaceAngle);
			TerrainTraction = CalculateTraction(TerrainNormal, maxTerrainAngle);
			ApplyGravity();
		}

		private void ApplyGravity()
		{
			if (!Grounded || SurfaceTraction.Approx(0f))
			{
				wrapper.Rigidbody.AddForce(Vector3.down * gravity, ForceMode.Acceleration);
			}
		}

		/// <summary>
		/// Check if grounded.
		/// </summary>
		private void GroundCheck()
		{
			Grounded = false;
			Vector3 origin = wrapper.Position + wrapper.Up * groundOffset;
			if (Physics.SphereCast(origin, groundRadius, -wrapper.Up, out groundedHit, groundOffset + groundReach, layerMask))
			{
				Grounded = true;
				if(debug)
				{
					Debug.DrawLine(origin, groundedHit.point, Color.red);
				}
			}
		}

		/// <summary>
		/// Move to average point on terrain.
		/// </summary>
		private void StepCheck()
		{
			if (!Grounded)
			{
				StepPoint = wrapper.Position + wrapper.Velocity.ClampMagnitude(stepHeight * 0.9f);
				return;
			}

			Vector3 origin = wrapper.Position + wrapper.Up * stepHeight;
			if (!PhysicsUtils.TubeCast(origin, stepRadius, -wrapper.Up, wrapper.Forward, stepHeight * 2f, layerMask, rayCount, out List<RaycastHit> hits, true))
			{
				SurfaceNormal = Vector3.Lerp(SurfaceNormal, groundedHit.normal, normalSmoothing * Time.fixedDeltaTime);
				TerrainNormal = Vector3.Lerp(TerrainNormal, wrapper.Up, normalSmoothing * Time.fixedDeltaTime);
				return;
			}

			stepNormals = hits.Select(h => h.normal).ToArray();
			SurfaceNormal = Vector3.Lerp(SurfaceNormal, stepNormals.AverageDirection(), normalSmoothing * Time.fixedDeltaTime);

			stepPoints = hits.Select(h => h.point).ToArray();
			TerrainNormal = Vector3.Lerp(TerrainNormal, stepPoints.ApproxNormalFromPoints(wrapper.Up, out Vector3 center, debug, debugSize), normalSmoothing * Time.fixedDeltaTime);

			StepPoint = Vector3.Lerp(StepPoint, center, stepSmooth * Time.fixedDeltaTime);

			if (debug)
			{
				Debug.DrawRay(StepPoint, TerrainNormal * debugSize, Color.magenta);
			}

			wrapper.Position = wrapper.Position.SetY(StepPoint.y);
			//float grip = wrapper.Velocity.normalized.Dot(SurfaceNormal) < 0f ? surfaceTractionCurve.Evaluate(SurfaceTraction * surfaceGrip) : 1f;
			wrapper.Velocity = wrapper.Velocity.Disperse(SurfaceNormal);// * grip;

			if (debug)
			{
				foreach (RaycastHit hit in hits)
				{
					Debug.DrawLine(origin, hit.point, Color.blue);
				}
			}
		}

		private float CalculateTraction(Vector3 normal, float maxAngle)
		{
			if (!Grounded)
			{
				return 0f;
			}

			float angle = Vector3.Angle(wrapper.Up, normal);
			return angle > maxAngle ? 0f : Mathf.Clamp01(angle / 90f).Invert();
		}
	}
}
