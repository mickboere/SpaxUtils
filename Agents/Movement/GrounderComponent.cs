using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Component responsible for grounding an agent, allowing it to traverse slopes and stairs.
	/// </summary>
	[DefaultExecutionOrder(-200)]
	public class GrounderComponent : EntityComponentMono
	{
		[Serializable]
		private class OptimizationSettings
		{
			[Serializable]
			public struct Settings
			{
				public int RayCount;

				public Settings(int rayCount)
				{
					RayCount = rayCount;
				}
			}

			public Settings Culled;
			public Settings Low;
			public Settings Medium;
			public Settings High;
			public Settings Top;

			public OptimizationSettings(int culled, int low, int medium, int high, int top)
			{
				Culled = new Settings(culled);
				Low = new Settings(low);
				Medium = new Settings(medium);
				High = new Settings(high);
				Top = new Settings(top);
			}

			public Settings Get(PriorityLevel prio)
			{
				switch (prio)
				{
					case PriorityLevel.Top:
						return Top;
					case PriorityLevel.High:
						return High;
					case PriorityLevel.Medium:
						return Medium;
					case PriorityLevel.Low:
						return Low;
					default:
						return Culled;
				}
			}
		}

		/// <summary>
		/// Whether this entity should ground itself.
		/// </summary>
		public bool Ground { get; set; } = true;

		/// <summary>
		/// Returns true when the entity is touching the ground.
		/// </summary>
		public bool Grounded { get; private set; }

		/// <summary>
		/// Grounded percentage: 1 = fully grounded, 0 = ground exceeds reach.
		/// </summary>
		public float GroundedAmount { get; private set; }

		/// <summary>
		/// The amount of gravitational force applied to the agent.
		/// </summary>
		public CompositeFloat Gravity { get; private set; }

		/// <summary>
		/// Normal vector of the average surface normal.
		/// </summary>
		public Vector3 SurfaceNormal { get; private set; }

		/// <summary>
		/// Normalized slope of the average surface normal.
		/// </summary>
		public float SurfaceSlope { get; private set; }

		/// <summary>
		/// The grip on the current surface.
		/// When too low, <see cref="Sliding"/> will be true.
		/// </summary>
		public float Traction { get; private set; }

		/// <summary>
		/// Returns true when the entity is unable to move due to too low <see cref="Traction"/>.
		/// </summary>
		public bool Sliding => Grounded && Traction <= slidingThreshold;

		/// <summary>
		/// Normal vector of the averaged terrain height.
		/// </summary>
		public Vector3 TerrainNormal { get; private set; }

		/// <summary>
		/// Normalized slope of the average terrain height.
		/// </summary>
		public float TerrainSlope { get; private set; }

		/// <summary>
		/// The ability to move on the current terrain.
		/// Running down-hill may increase mobility, up-hill decrease. Stuff like walking in water may also decrease it.
		/// </summary>
		public float Mobility { get; private set; }

		/// <summary>
		/// The added elevation to the grounded position.
		/// </summary>
		public float Elevation { get; set; }

		/// <summary>
		/// The average elevation point of the grounding terrain.
		/// </summary>
		public Vector3 StepPoint { get; private set; }

		[SerializeField] private LayerMask layerMask;
		[SerializeField] private float gravity = 9.8f;
		[SerializeField] private OptimizationSettings settings = new OptimizationSettings(1, 3, 5, 8, 16);
		[Header("Grounding")]
		[SerializeField] private float groundOffset = 1f;
		[SerializeField] private float groundReach = 0.25f;
		[SerializeField] private float groundRadius = 0.2f;
		[SerializeField] private float jumpThreshold = 4f;
		[Header("Stepping")]
		[SerializeField] private float stepHeight = 1f;
		[SerializeField] private Vector2 stepRadius = new Vector2(0.25f, 0.25f);
		[SerializeField, Range(0f, 20f)] private float stepSmooth = 20f;
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
		private IAgent agent;

		private RaycastHit groundedHit;
		private Vector3[] stepPoints;
		private Vector3[] stepNormals;

		public void InjectDependencies(RigidbodyWrapper rigidbodyWrapper, IAgent agent)
		{
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.agent = agent;
		}

		protected void Awake()
		{
			Gravity = new CompositeFloat(gravity);
			SurfaceNormal = rigidbodyWrapper.Up;
			TerrainNormal = rigidbodyWrapper.Up;
		}

		protected void FixedUpdate()
		{
			GroundCheck();
			StepCheck();
			CalculateTraction();
			ApplyForces();
			BlockActor();
		}

		private void GroundCheck()
		{
			Grounded = false;
			GroundedAmount = 0f;
			Vector3 origin = rigidbodyWrapper.Position + rigidbodyWrapper.Up * groundOffset;
			if (Physics.SphereCast(origin, groundRadius, -rigidbodyWrapper.Up, out groundedHit, groundOffset + groundReach, layerMask))
			{
				Grounded = true;
				GroundedAmount = Mathf.Clamp01((groundedHit.distance - groundOffset) / groundReach).Invert();
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
			Vector3 origin = rigidbodyWrapper.Position + SurfaceNormal * stepHeight;
			Vector2 radius = new Vector2(
				Mathf.Max(stepRadius.x, rigidbodyWrapper.RelativeVelocity.x * stepRadius.x * 0.5f),
				Mathf.Max(stepRadius.y, rigidbodyWrapper.RelativeVelocity.z * stepRadius.y * 0.5f));
			if (!PhysicsUtils.TubeCast(origin, radius, -SurfaceNormal, rigidbodyWrapper.Forward, stepHeight * 2f,
				layerMask, settings.Get(Entity.Priority).RayCount, out List<RaycastHit> hits, true, debug))
			{
				SurfaceNormal = Vector3.Slerp(SurfaceNormal, groundedHit.normal, normalSmoothing * Time.fixedDeltaTime);
				TerrainNormal = Vector3.Slerp(TerrainNormal, rigidbodyWrapper.Up, normalSmoothing * Time.fixedDeltaTime);
				StepPoint = rigidbodyWrapper.Position;
				return;
			}

			// Calculate surface (traction)
			stepNormals = hits.Select(h => h.normal).ToArray();
			SurfaceNormal = Vector3.Slerp(SurfaceNormal, stepNormals.AverageDirection(), normalSmoothing * Time.fixedDeltaTime);

			// Calculate terrain (mobility)
			stepPoints = hits.Select(h => h.point).ToArray();
			TerrainNormal = Vector3.Slerp(TerrainNormal, stepPoints.ApproxNormalFromPoints(rigidbodyWrapper.Up,
				out Vector3 center, debug, debugSize), normalSmoothing * Time.fixedDeltaTime);

			// Calculate desired step-position.
			StepPoint = rigidbodyWrapper.Position +
				Vector3.Lerp(StepPoint - rigidbodyWrapper.Position,
				center - rigidbodyWrapper.Position,
				stepSmooth * rigidbodyWrapper.Velocity.y.Abs().Clamp(1f, 10f) * Time.fixedDeltaTime);

			if (debug)
			{
				Debug.DrawRay(StepPoint, TerrainNormal * debugSize, Color.magenta);

				//foreach (RaycastHit hit in hits)
				//{
				//	Debug.DrawLine(origin, hit.point, Color.blue);
				//}
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
			if (!Ground)
			{
				return;
			}

			if (Grounded && rigidbodyWrapper.Velocity.y < jumpThreshold)
			{
				if (Sliding)
				{
					// Slide down slope.
					Vector3 slidingForce = (Vector3.down * Gravity).ProjectOnPlane(TerrainNormal);
					slidingForce *= Mathf.Clamp(rigidbodyWrapper.Velocity.y * 10f, 1f, 10f); // Upward-sliding resistance.
					rigidbodyWrapper.AddForce(slidingForce * SurfaceSlope, ForceMode.Acceleration);
				}
				else
				{
					// Disperse along terrain normal.
					Vector3 movementDispersion = rigidbodyWrapper.Velocity.DisperseOnPlane(TerrainNormal) * Mobility;
					rigidbodyWrapper.AddForce(movementDispersion, ForceMode.VelocityChange);
				}

				// Glue to ground.
				rigidbodyWrapper.Position = rigidbodyWrapper.Position.SetY(StepPoint.y + Elevation);
			}
			else
			{
				rigidbodyWrapper.AddForce(Vector3.down * Gravity, ForceMode.Acceleration);
			}
		}

		private void BlockActor()
		{
			// Block agent from acting while not grounded or sliding.
			if (!Grounded || Sliding)
			{
				agent.Actor.AddBlocker(this);
			}
			else
			{
				agent.Actor.RemoveBlocker(this);
			}
		}
	}
}
