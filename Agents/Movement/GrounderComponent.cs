using System;
using System.Collections.Generic;
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
		/// Invoked when the agent transitions from airborne to grounded.
		/// Parameter is the peak downward speed (m/s) recorded during the current fall phase.
		/// Peak resets whenever the agent gains upward velocity (double jump, ability, etc).
		/// </summary>
		public event Action<float> LandedEvent;

		/// <summary>
		/// Whether this entity should ground itself.
		/// </summary>
		public bool Ground { get; set; } = true;

		/// <summary>
		/// Returns true when the entity is touching the ground or within the coyote grace period.
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
		/// Normal vector of the average surface normal (from geometry faces).
		/// </summary>
		public Vector3 SurfaceNormal { get; private set; }

		/// <summary>
		/// Normalized slope of the average surface normal (0 = flat, 1 = vertical).
		/// </summary>
		public float SurfaceSlope { get; private set; }

		/// <summary>
		/// Returns true when the agent is actively sliding down a slope.
		/// Uses a static/dynamic friction model: static friction must be overcome to start sliding,
		/// and dynamic friction (lower) must slow the agent enough to stop.
		/// Stairs are detected via surface-terrain slope divergence and excluded from sliding.
		/// </summary>
		public bool Sliding => Grounded && isSliding;

		/// <summary>
		/// Smooth 0-1 value representing sliding intensity.
		/// Transitions softly around slide start and recovery thresholds.
		/// Useful for animation blending and force interpolation.
		/// </summary>
		public float SlidingAmount { get; private set; }

		/// <summary>
		/// The configured static friction angle in degrees.
		/// Exposed for external consumers (e.g. movement handler braking authority).
		/// </summary>
		public float StaticFrictionAngle => staticFrictionAngle;

		/// <summary>
		/// Normal vector of the averaged terrain height (from hit point positions).
		/// </summary>
		public Vector3 TerrainNormal { get; private set; }

		/// <summary>
		/// Normalized slope of the average terrain height (0 = flat, 1 = vertical).
		/// </summary>
		public float TerrainSlope { get; private set; }

		/// <summary>
		/// The ability to move on the current terrain.
		/// Running down-hill may increase mobility, up-hill decrease.
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

		/// <summary>
		/// The last position where the agent was safely grounded.
		/// </summary>
		public Vector3 LastSafePosition { get; private set; }

		[SerializeField] private LayerMask layerMask;
		[SerializeField] private float gravity = 9.8f;
		[SerializeField] private OptimizationSettings settings = new OptimizationSettings(1, 3, 5, 8, 16);

		[Header("Grounding")]
		[SerializeField] private float groundOffset = 1f;
		[SerializeField] private float groundReach = 0.25f;
		[SerializeField] private float groundRadius = 0.2f;
		[SerializeField] private float jumpThreshold = 2.5f;
		[SerializeField, Tooltip("Seconds the agent stays grounded after losing ground contact.")]
		private float coyoteTime = 0.1f;

		[Header("Stepping")]
		[SerializeField] private float stepHeight = 1f;
		[SerializeField] private Vector2 stepRadius = new Vector2(0.5f, 0.5f);
		[SerializeField, Range(0f, 20f)] private float stepSmooth = 20f;

		[Header("Surface & Mobility")]
		[SerializeField, Range(0f, 20f)] private float normalSmoothing = 12.5f;
		[SerializeField, Range(0f, 90f)] private float maxTerrainAngle = 60f;
		[SerializeField, Range(0f, 2f)] private float mobility = 1f;
		[SerializeField] private AnimationCurve mobilityCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[SerializeField, Range(0f, 20f), Tooltip("How quickly mobility increases (easier terrain).")]
		private float mobilityRampUp = 10f;
		[SerializeField, Range(0f, 20f), Tooltip("How slowly mobility decreases (harder terrain). Lower = lazier.")]
		private float mobilityRampDown = 3f;

		[Header("Sliding (Static/Dynamic Friction)")]
		[SerializeField, Range(0f, 90f), Tooltip("Terrain angle above which static friction is overcome and sliding begins.")]
		private float staticFrictionAngle = 40f;
		[SerializeField, Range(0f, 90f), Tooltip("Terrain angle below which the agent can recover from sliding.")]
		private float dynamicFrictionAngle = 25f;
		[SerializeField, Range(0f, 1f), Tooltip("Friction coefficient while sliding. Lower = more slippery.")]
		private float dynamicFriction = 0.3f;
		[SerializeField, Tooltip("Slide speed below which the agent can stop sliding (when terrain is below dynamic friction angle).")]
		private float slideExitSpeed = 0.5f;
		[SerializeField, Range(0f, 1f), Tooltip("How much horizontal speed (m/s, normalized) contributes to breaking static friction. " +
			"High speed on a moderate slope can cause a slip.")]
		private float speedSlideContribution = 0.1f;
		[SerializeField, Range(0f, 1f), Tooltip("Minimum divergence between surface and terrain slope to detect stairs. " +
			"When divergence exceeds this, the terrain is treated as stairs and sliding is suppressed.")]
		private float stairDivergenceThreshold = 0.2f;
		[SerializeField, Range(0f, 20f), Tooltip("How quickly SlidingAmount ramps up when entering a slide.")]
		private float slidingRampUp = 10f;
		[SerializeField, Range(0f, 20f), Tooltip("How slowly SlidingAmount ramps down when exiting a slide.")]
		private float slidingRampDown = 4f;

		[Header("Landing Impact")]
		[SerializeField, Tooltip("Minimum falling speed (m/s) before horizontal velocity is absorbed on landing.")]
		private float impactAbsorptionMinSpeed = 3f;
		[SerializeField, Tooltip("Falling speed (m/s) at which all horizontal velocity is absorbed on landing.")]
		private float impactAbsorptionMaxSpeed = 10f;
		[SerializeField, Tooltip("Seconds airborne below which impact is scaled down (slope transitions, small bumps).")]
		private float impactFullAirborneTime = 0.3f;

		[Header("Debug")]
		[SerializeField] private bool debug;
		[SerializeField, Conditional(nameof(debug))] private float debugSize = 0.25f;

		private RigidbodyWrapper rigidbodyWrapper;
		private IAgent agent;

		private RaycastHit groundedHit;
		private Vector3[] stepPoints;
		private Vector3[] stepNormals;

		// Landing detection.
		private bool wasGrounded = true;
		private float peakFallingSpeed;
		private bool landedThisFrame;
		private float airborneDuration;

		// Coyote time.
		private bool groundContact;
		private float airborneTimer;

		// Sliding state.
		private bool isSliding;
		private bool wasSliding;

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
			LastSafePosition = rigidbodyWrapper.Position;
		}

		protected void FixedUpdate()
		{
			GroundCheck();
			DetectLanding();
			StepCheck();
			CalculateSurface();
			UpdateSlidingState();
			ApplyForces();
			BlockActor();
			CheckIfSafe();
		}

		private void GroundCheck()
		{
			groundContact = false;
			GroundedAmount = 0f;
			Vector3 origin = rigidbodyWrapper.Position + rigidbodyWrapper.Up * groundOffset;
			if (Physics.SphereCast(origin, groundRadius, -rigidbodyWrapper.Up, out groundedHit, groundOffset + groundReach, layerMask))
			{
				groundContact = true;
				GroundedAmount = Mathf.Clamp01((groundedHit.distance - groundOffset) / groundReach).Invert();
				if (debug)
				{
					Debug.DrawLine(origin, groundedHit.point, Color.red);
				}
			}

			// Coyote time: stay grounded for a short duration after losing ground contact.
			if (groundContact)
			{
				airborneTimer = 0f;
				Grounded = true;
			}
			else if (Grounded)
			{
				airborneTimer += Time.fixedDeltaTime;
				if (airborneTimer > coyoteTime || rigidbodyWrapper.Velocity.y > jumpThreshold)
				{
					// Grace period expired, or agent is jumping upward.
					Grounded = false;
				}
			}
		}

		/// <summary>
		/// Detects the transition from airborne to grounded and fires <see cref="LandedEvent"/>.
		/// Tracks peak downward speed while airborne to provide reliable impact data
		/// even if physics collision resolution zeroes out velocity before detection.
		/// Peak resets when the agent gains upward velocity (double jump, slow-fall ability, etc).
		/// Impact is scaled by airborne duration to reduce false positives from slope transitions.
		/// Uses raw ground contact, not coyote-grounded, for accurate landing detection.
		/// </summary>
		private void DetectLanding()
		{
			landedThisFrame = false;

			if (!groundContact)
			{
				airborneDuration += Time.fixedDeltaTime;

				float verticalSpeed = rigidbodyWrapper.Velocity.y;

				if (verticalSpeed > 0f)
				{
					// Agent is moving upward (jumped, double jumped, ability, etc).
					// Reset peak so only the current fall phase counts.
					peakFallingSpeed = 0f;
				}
				else
				{
					// Track peak downward speed while falling.
					float downSpeed = -verticalSpeed;
					if (downSpeed > peakFallingSpeed)
					{
						peakFallingSpeed = downSpeed;
					}
				}
			}
			else if (!wasGrounded)
			{
				// Just transitioned from airborne to grounded.
				// Scale impact by surface alignment: flat ground = full impact, steep slope = glancing.
				float surfaceAlignment = Mathf.Clamp01(Vector3.Dot(groundedHit.normal, rigidbodyWrapper.Up));
				float effectiveImpact = peakFallingSpeed * surfaceAlignment;

				// Scale impact by airborne duration.
				// Brief airborne moments (slope transitions, bumps) produce reduced impact.
				// Real falls accumulate enough airborne time to reach full impact.
				float airborneFactor = Mathf.Clamp01(airborneDuration / impactFullAirborneTime);
				effectiveImpact *= airborneFactor;

				// Absorb horizontal velocity proportional to impact severity.
				float severity = Mathf.InverseLerp(impactAbsorptionMinSpeed, impactAbsorptionMaxSpeed, effectiveImpact);
				if (severity > 0f)
				{
					Vector3 vel = rigidbodyWrapper.Velocity;
					Vector3 horizontal = new Vector3(vel.x, 0f, vel.z);
					rigidbodyWrapper.Velocity = vel - horizontal * Mathf.Clamp01(severity);
				}

				landedThisFrame = true;
				LandedEvent?.Invoke(effectiveImpact);
				peakFallingSpeed = 0f;
				airborneDuration = 0f;
			}

			wasGrounded = groundContact;
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

			// Filter out overhang/underside hits. A hit whose normal faces away from up
			// is a ceiling or cliff underside and should not contribute to surface or terrain data.
			// This prevents pointed cliff edges from averaging top and bottom surfaces into a false flat.
			int validCount = 0;
			for (int i = 0; i < hits.Count; i++)
			{
				if (Vector3.Dot(hits[i].normal, rigidbodyWrapper.Up) > 0f)
				{
					// Swap valid hit to front of list.
					if (i != validCount)
					{
						RaycastHit temp = hits[validCount];
						hits[validCount] = hits[i];
						hits[i] = temp;
					}
					validCount++;
				}
			}

			// If no valid hits remain after filtering, fall back to ground hit data.
			if (validCount == 0)
			{
				SurfaceNormal = Vector3.Slerp(SurfaceNormal, groundedHit.normal, normalSmoothing * Time.fixedDeltaTime);
				TerrainNormal = Vector3.Slerp(TerrainNormal, rigidbodyWrapper.Up, normalSmoothing * Time.fixedDeltaTime);
				StepPoint = rigidbodyWrapper.Position;
				return;
			}

			// Calculate surface normals from valid hits only.
			if (stepNormals == null || stepNormals.Length != validCount)
			{
				stepNormals = new Vector3[validCount];
			}
			for (int i = 0; i < validCount; i++)
			{
				stepNormals[i] = hits[i].normal;
			}
			SurfaceNormal = Vector3.Slerp(SurfaceNormal, stepNormals.AverageDirection(), normalSmoothing * Time.fixedDeltaTime);

			// Calculate terrain points from valid hits only.
			if (stepPoints == null || stepPoints.Length != validCount)
			{
				stepPoints = new Vector3[validCount];
			}
			for (int i = 0; i < validCount; i++)
			{
				stepPoints[i] = hits[i].point;
			}
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
			}
		}

		/// <summary>
		/// Calculates surface slope, terrain slope, and mobility.
		/// Mobility includes a directional component: going downhill on steep terrain
		/// is easier than going uphill.
		/// </summary>
		private void CalculateSurface()
		{
			if (!Grounded)
			{
				SurfaceSlope = 0f;
				Mobility = 0f;
				TerrainSlope = 0f;
				return;
			}

			// Surface slope (from geometry faces, noisy on stairs/detail).
			float surfaceAngle = Vector3.Angle(rigidbodyWrapper.Up, SurfaceNormal);
			SurfaceSlope = Mathf.Clamp01(surfaceAngle / 90f);

			// Terrain slope (from hit point positions, stable across stair geometry).
			float terrainAngle = Vector3.Angle(rigidbodyWrapper.Up, TerrainNormal);
			float targetMobility = mobilityCurve.Evaluate(Mathf.Clamp01(terrainAngle / maxTerrainAngle * mobility).Invert());
			TerrainSlope = Mathf.Clamp01(terrainAngle / 90f);

			// Directional mobility: going downhill is easier, going uphill is harder.
			// Scaled by base mobility so the penalty doesn't create a dead zone
			// when the mobility curve has already reduced movement.
			Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, TerrainNormal).normalized;
			Vector3 flatVel = rigidbodyWrapper.Velocity.FlattenY();
			if (flatVel.magnitude > 0.1f && downhill.magnitude > 0.01f)
			{
				float downhillDot = Vector3.Dot(flatVel.normalized, downhill.FlattenY().normalized);
				if (downhillDot > 0f)
				{
					// Downhill: boost mobility.
					targetMobility += downhillDot * TerrainSlope;
				}
				else
				{
					// Uphill: reduce mobility, but only proportional to remaining mobility
					// to avoid a dead zone where the agent can't move at all yet can't slide either.
					targetMobility *= 1f + downhillDot * TerrainSlope;
					targetMobility = Mathf.Max(0.05f, targetMobility);
				}
			}

			// Asymmetric smoothing: mobility increases quickly (stepping onto easier terrain)
			// but decreases slowly (absorbs jitter from stairs, debris, uneven ground).
			float speed = targetMobility > Mobility ? mobilityRampUp : mobilityRampDown;
			Mobility = Mathf.Lerp(Mobility, targetMobility, speed * Time.fixedDeltaTime);
		}

		/// <summary>
		/// Static/dynamic friction model for sliding.
		/// Static friction (high threshold) must be overcome to start sliding.
		/// Dynamic friction (low threshold) keeps the agent sliding until they slow down enough
		/// on sufficiently flat terrain.
		/// Stairs are detected via surface-terrain slope divergence: when surface slope is much higher
		/// than terrain slope, the geometry is stairs/detail and sliding is suppressed.
		/// </summary>
		private void UpdateSlidingState()
		{
			wasSliding = isSliding;

			if (!Grounded)
			{
				isSliding = false;
				SlidingAmount = 0f;
				return;
			}

			float terrainAngle = Vector3.Angle(rigidbodyWrapper.Up, TerrainNormal);

			// Stair detection: if surface slope diverges significantly from terrain slope,
			// the agent is on stairs or detailed geometry and should not slide.
			// On a real slope, surface and terrain slopes are roughly equal.
			// On stairs, surface slope is much higher (risers are near vertical).
			float slopeDivergence = SurfaceSlope - TerrainSlope;
			bool isStairs = slopeDivergence > stairDivergenceThreshold;

			if (!isSliding)
			{
				// Cannot enter sliding on stairs.
				if (!isStairs)
				{
					// Check if static friction is overcome.
					// Horizontal speed contributes: running fast across a steep slope can break traction.
					float horizontalSpeed = rigidbodyWrapper.Velocity.FlattenY().magnitude;
					float effectiveAngle = terrainAngle + horizontalSpeed * speedSlideContribution;
					if (effectiveAngle > staticFrictionAngle)
					{
						isSliding = true;
					}
				}
			}
			else
			{
				// Already sliding. Only exit when terrain is flat enough and speed is low.
				// Stair detection is NOT used for exit - cliff edges and geometry seams
				// can produce high surface divergence that would falsely exit the slide.
				float slideSpeed = rigidbodyWrapper.Velocity.ProjectOnPlane(TerrainNormal).magnitude;
				if (terrainAngle < dynamicFrictionAngle && slideSpeed < slideExitSpeed)
				{
					isSliding = false;
				}
			}

			// Smooth SlidingAmount toward target.
			float target = isSliding ? 1f : 0f;
			float rampSpeed = target > SlidingAmount ? slidingRampUp : slidingRampDown;
			SlidingAmount = Mathf.MoveTowards(SlidingAmount, target, rampSpeed * Time.fixedDeltaTime);
		}

		private void ApplyForces()
		{
			if (!Ground)
			{
				return;
			}

			if (Grounded && rigidbodyWrapper.Velocity.y < jumpThreshold)
			{
				if (isSliding)
				{
					// Gravity projected along slope surface (downslope acceleration).
					Vector3 downslope = (Vector3.down * Gravity).ProjectOnPlane(TerrainNormal);
					rigidbodyWrapper.AddForce(downslope, ForceMode.Acceleration);

					// Dynamic friction opposing slide velocity.
					Vector3 slideVelocity = rigidbodyWrapper.Velocity.ProjectOnPlane(TerrainNormal);
					if (slideVelocity.magnitude > 0.01f)
					{
						float normalForce = Gravity * (1f - TerrainSlope);
						Vector3 frictionForce = -slideVelocity.normalized * dynamicFriction * normalForce;
						rigidbodyWrapper.AddForce(frictionForce, ForceMode.Acceleration);
					}
				}
				else if (!landedThisFrame && !wasSliding)
				{
					// Disperse along terrain normal.
					// Skipped on landing frame and slide-exit frame to prevent velocity spikes.
					Vector3 movementDispersion = rigidbodyWrapper.Velocity.DisperseOnPlane(TerrainNormal) * Mobility;
					rigidbodyWrapper.AddForce(movementDispersion, ForceMode.VelocityChange);
				}

				if (!isSliding)
				{
					// When not sliding, zero downward vertical velocity proportional to grounded contact.
					Vector3 vel = rigidbodyWrapper.Velocity;
					if (vel.y < 0f)
					{
						rigidbodyWrapper.Velocity = new Vector3(vel.x, vel.y * (1f - GroundedAmount), vel.z);
					}
				}

				// Glue to ground, scaled by grounded amount.
				// When barely grounded (edge of cliff), the snap weakens and gravity takes over.
				// Always active, including while sliding, as the agent has no ground collider.
				float targetY = StepPoint.y + Elevation;
				float snappedY = Mathf.Lerp(rigidbodyWrapper.Position.y, targetY, GroundedAmount);
				rigidbodyWrapper.Position = rigidbodyWrapper.Position.SetY(snappedY);

				// Apply partial gravity when not fully grounded (cliff edges, ledges).
				if (GroundedAmount < 0.99f)
				{
					rigidbodyWrapper.AddForce(Vector3.down * Gravity * (1f - GroundedAmount), ForceMode.Acceleration);
				}
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

		private void CheckIfSafe()
		{
			if (Ground &&
				Grounded &&
				rigidbodyWrapper.Control.Value.Approx(1f) &&
				TerrainSlope < 0.33f &&
				!Sliding &&
				GroundedAmount.Approx(1f) &&
				!agent.Actor.Blocked)
			{
				LastSafePosition = rigidbodyWrapper.Position;
			}
		}
	}
}
