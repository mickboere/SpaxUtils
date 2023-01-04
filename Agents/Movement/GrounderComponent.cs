using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Component responsible for grounding an entity, allowing it to traverse slopes and stairs.
	/// </summary>
	[DefaultExecutionOrder(-200)]
	public class GrounderComponent : EntityComponentBase, IGrounderComponent
	{
		/// <inheritdoc/>
		public bool Grounded { get; private set; }

		/// <inheritdoc/>
		public bool Sliding => Grounded && Traction <= slidingThreshold;

		/// <inheritdoc/>
		public float SurfaceSlope { get; private set; }

		/// <inheritdoc/>
		public float TerrainSlope { get; private set; }

		/// <inheritdoc/>
		public float Traction { get; private set; }

		/// <inheritdoc/>
		public float Mobility { get; private set; }

		public Vector3 SurfaceNormal { get; private set; }
		public Vector3 TerrainNormal { get; private set; }
		public Vector3 StepPoint { get; private set; }

		[SerializeField] private LayerMask layerMask;
		[SerializeField] private float gravity = 9f;
		[Header("Grounding")]
		[SerializeField] private float groundOffset = 1f;
		[SerializeField] private float groundReach = 0.25f;
		[SerializeField] private float groundRadius = 0.2f;
		[Header("Stepping")]
		[SerializeField] private float stepHeight = 1f;
		[SerializeField] private Vector2 stepRadius = new Vector2(0.25f, 0.25f);
		[SerializeField, Range(0f, 20f)] private float stepSmooth = 20f;
		[SerializeField] private int rayCount = 8;
		[Header("Traction")]
		[SerializeField, Range(0f, 20f)] private float normalSmoothing = 5f;
		[SerializeField, Range(0f, 90f)] private float maxSurfaceAngle = 90f;
		[SerializeField, Range(0f, 2f)] private float traction = 1f;
		[SerializeField] private AnimationCurve tractionCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[SerializeField, Range(0f, 1f)] private float slidingThreshold = 0f;
		[Header("Mobility")]
		[SerializeField, Range(0f, 90f)] private float maxTerrainAngle = 90f;
		[SerializeField, Range(0f, 2f)] private float mobility = 1f;
		[SerializeField] private AnimationCurve mobilityCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[Header("Debug")]
		[SerializeField] private bool debug;
		[SerializeField, Conditional(nameof(debug))] private float debugSize = 0.25f;

		private RigidbodyWrapper rigidbodyWrapper;

		private RaycastHit groundedHit;
		private Vector3[] stepPoints;
		private Vector3[] stepNormals;

		public void InjectDependencies(RigidbodyWrapper rigidbodyWrapper)
		{
			this.rigidbodyWrapper = rigidbodyWrapper;
		}

		protected void FixedUpdate()
		{
			GroundCheck();
			StepCheck();
			CalculateTraction();
			ApplyForces();
		}

		private void GroundCheck()
		{
			Grounded = false;
			Vector3 origin = rigidbodyWrapper.Position + rigidbodyWrapper.Up * groundOffset;
			if (Physics.SphereCast(origin, groundRadius, -rigidbodyWrapper.Up, out groundedHit, groundOffset + groundReach, layerMask))
			{
				Grounded = true;
				if (debug)
				{
					Debug.DrawLine(origin, groundedHit.point, Color.red);
				}
			}
		}

		private void StepCheck()
		{
			if (!Grounded)
			{
				StepPoint = rigidbodyWrapper.Position + rigidbodyWrapper.Velocity.ClampMagnitude(stepHeight * 0.9f);
				return;
			}

			if (StepPoint == Vector3.zero)
			{
				StepPoint = rigidbodyWrapper.Position;
			}

			// Map ground surface.
			Vector3 origin = rigidbodyWrapper.Position + rigidbodyWrapper.Up * stepHeight;
			Vector2 radius = new Vector2(
				Mathf.Max(stepRadius.x, rigidbodyWrapper.RelativeVelocity.x * stepRadius.x * 0.5f),
				Mathf.Max(stepRadius.y, rigidbodyWrapper.RelativeVelocity.z * stepRadius.y * 0.5f));

			if (!PhysicsUtils.TubeCast(origin, radius, -rigidbodyWrapper.Up, rigidbodyWrapper.Forward, stepHeight * 2f, layerMask, rayCount, out List<RaycastHit> hits, true))
			{
				SurfaceNormal = Vector3.Lerp(SurfaceNormal, groundedHit.normal, normalSmoothing * Time.fixedDeltaTime);
				TerrainNormal = Vector3.Lerp(TerrainNormal, rigidbodyWrapper.Up, normalSmoothing * Time.fixedDeltaTime);
				return;
			}

			// Calculate surface (traction)
			stepNormals = hits.Select(h => h.normal).ToArray();
			SurfaceNormal = Vector3.Lerp(SurfaceNormal, stepNormals.AverageDirection(), normalSmoothing * Time.fixedDeltaTime);

			// Calculate terrain (mobility)
			stepPoints = hits.Select(h => h.point).ToArray();
			TerrainNormal = Vector3.Lerp(TerrainNormal, stepPoints.ApproxNormalFromPoints(rigidbodyWrapper.Up, out Vector3 center, debug, debugSize), normalSmoothing * Time.fixedDeltaTime);

			// Calculate desired position.
			StepPoint = rigidbodyWrapper.Position + Vector3.Lerp(StepPoint - rigidbodyWrapper.Position, center - rigidbodyWrapper.Position, stepSmooth * Time.fixedDeltaTime);

			if (debug)
			{
				Debug.DrawRay(StepPoint, TerrainNormal * debugSize, Color.magenta);

				foreach (RaycastHit hit in hits)
				{
					Debug.DrawLine(origin, hit.point, Color.blue);
				}
			}
		}

		private void CalculateTraction()
		{
			if (!Grounded)
			{
				Traction = 0f;
				SurfaceSlope = 0f;
				Mobility = 0f;
				TerrainSlope = 0f;
				return;
			}

			// Walk slowly up and down stairs.
			//		Decrease mobility depending on slope.
			// Walk slowly up slopes and increasingly fast down.
			//		Decrease traction depending on slope.
			//		Lower traction should increase downwards slope speed over time.

			// Traction
			float surfaceAngle = Vector3.Angle(rigidbodyWrapper.Up, SurfaceNormal);
			Traction = tractionCurve.Evaluate(Mathf.Clamp01(surfaceAngle / maxSurfaceAngle * traction).Invert());
			SurfaceSlope = Mathf.Clamp01(surfaceAngle / 90f);

			// Mobility
			float terrainAngle = Vector3.Angle(rigidbodyWrapper.Up, TerrainNormal);
			Mobility = mobilityCurve.Evaluate(Mathf.Clamp01(terrainAngle / maxTerrainAngle * mobility).Invert());
			TerrainSlope = Mathf.Clamp01(terrainAngle / 90f);

			// Down-slope
			Mobility += Mathf.Clamp01((1f - Traction) * rigidbodyWrapper.Forward.Dot(SurfaceNormal));
		}

		private void ApplyForces()
		{
			if (Grounded)
			{
				if (Sliding)
				{
					Vector3 slidingForce = (Vector3.down * gravity).ProjectOnPlane(TerrainNormal);
					rigidbodyWrapper.AddForce(slidingForce * SurfaceSlope, ForceMode.Acceleration);
				}
				else
				{
					Vector3 movementDispersion = rigidbodyWrapper.Velocity.DisperseOnPlane(TerrainNormal) * Mobility;
					rigidbodyWrapper.AddForce(movementDispersion, ForceMode.VelocityChange);
				}

				rigidbodyWrapper.Position = rigidbodyWrapper.Position.SetY(StepPoint.y);
			}
			else
			{
				rigidbodyWrapper.AddForce(Vector3.down * gravity, ForceMode.Acceleration);
			}
		}
	}
}
