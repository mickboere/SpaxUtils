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

		private const float MIN_STEP_SUPPORT_TO_GROUND = 0.35f;
		private const float STEP_SUPPORT_MIN = 0.25f;
		private const float STEP_SUPPORT_MAX = 0.75f;

		/// <summary>
		/// Invoked when the agent transitions from airborne to grounded.
		/// Parameter is the peak downward speed (m/s) recorded during the current fall phase.
		/// Peak resets whenever the agent gains upward velocity (double jump, ability, etc).
		/// </summary>
		public event Action<float, RaycastHit> LandedEvent;

		/// <summary>
		/// Whether this entity should ground itself.
		/// </summary>
		public bool Ground { get; set; } = true;

		/// <summary>
		/// Returns true when the entity is currently touching the ground.
		/// </summary>
		public bool Grounded { get; private set; }

		/// <summary>
		/// Grounded percentage: 1 = fully grounded, 0 = ground exceeds reach.
		/// </summary>
		public float GroundedAmount { get; private set; }

		/// <summary>
		/// 0-1 estimate of how much of the intended step sample footprint is actually supported.
		/// Useful near ledges where only a minority of rays still hit valid ground.
		/// </summary>
		public float StepSupport { get; private set; }

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
		/// Whether the agent is in the jump phase (as opposed to falling).
		/// True from the moment of jump until the jump-to-fall timer expires and velocity is negative.
		/// Used by PoserNode to blend between jump and flying blend trees.
		/// </summary>
		public bool IsJumping { get; private set; }

		/// <summary>
		/// Whether the agent is currently in the process of landing.
		/// </summary>
		public bool IsLanding { get; private set; }

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
		[SerializeField] private float groundHeight = 1f;
		[SerializeField] private float groundReach = 0.45f;
		[SerializeField] private float groundRadius = 0.2f;
		[SerializeField] private float flyThreshold = 2.5f;

		[Header("Stepping")]
		[SerializeField] private float stepHeight = 1f;
		[SerializeField] private Vector2 stepRadius = new Vector2(0.5f, 0.5f);
		[SerializeField, Range(0f, 20f)] private float stepSmooth = 20f;
		[SerializeField, Range(0f, 90f), Tooltip("Ignore valid step hits whose slope angle exceeds the median by this many degrees. This helps reject near-wall hits beside cliffs from poisoning the ground average.")]
		private float steepHitOutlierAngle = 25f;
		[SerializeField, Tooltip("Maximum distance from the median hit height (along up axis) before a step hit is rejected as an outlier. Helps reject rays that punch through ledge edges to the ground below.")]
		private float stepHeightOutlierDistance = 0.5f;

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
		[SerializeField, Range(0f, 1f), Tooltip("How much horizontal speed (m/s, normalized) contributes to breaking static friction. High speed on a moderate slope can cause a slip.")]
		private float speedSlideContribution = 0.1f;
		[SerializeField, Range(0f, 1f), Tooltip("Minimum divergence between surface and terrain slope to detect stairs. When divergence exceeds this, the terrain is treated as stairs and sliding is suppressed.")]
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

		private RaycastHit groundedHit;
		private Vector3[] stepPoints;
		private Vector3[] stepNormals;

		// Landing detection.
		private bool wasGrounded = true;
		private float peakFallingSpeed;
		private bool landedThisFrame;
		private float airborneDuration;

		// Ground contact.
		private bool groundContact;

		// Sliding state.
		private bool isSliding;
		private bool wasSliding;

		// Reusable buffer for median computation in outlier filters.
		private float[] outlierBuffer;

		public void InjectDependencies(RigidbodyWrapper rigidbodyWrapper)
		{
			this.rigidbodyWrapper = rigidbodyWrapper;
		}

		protected void Awake()
		{
			Gravity = new CompositeFloat(gravity);
			SurfaceNormal = rigidbodyWrapper.Up;
			TerrainNormal = rigidbodyWrapper.Up;
			LastSafePosition = rigidbodyWrapper.Position;
			StepSupport = 1f;
		}

		protected void FixedUpdate()
		{
			GroundCheck();
			UpdateJumpState();
			StepCheck();
			UpdateGroundedState();
			DetectLanding();
			CalculateSurface();
			UpdateSlidingState();
			ApplyForces();
			CheckIfSafe();
		}

		private void GroundCheck()
		{
			groundContact = false;
			GroundedAmount = 0f;

			Vector3 origin = rigidbodyWrapper.Position + rigidbodyWrapper.Up * groundHeight;
			if (Physics.SphereCast(origin, groundRadius, -rigidbodyWrapper.Up, out groundedHit, groundHeight + groundReach, layerMask))
			{
				groundContact = true;
				GroundedAmount = Mathf.Clamp01((groundedHit.distance - groundHeight) / groundReach).Invert();
				if (debug)
				{
					Debug.DrawLine(origin, groundedHit.point, Color.red);
				}
			}
		}

		private void UpdateGroundedState()
		{
			if (IsJumping || !groundContact)
			{
				Grounded = false;
				return;
			}

			Grounded = StepSupport >= MIN_STEP_SUPPORT_TO_GROUND;
		}

		/// <summary>
		/// Detects the transition from airborne to grounded and fires <see cref="LandedEvent"/>.
		/// Tracks peak downward speed while airborne to provide reliable impact data
		/// even if physics collision resolution zeroes out velocity before detection.
		/// Peak resets when the agent gains upward velocity (double jump, slow-fall ability, etc).
		/// Impact is scaled by airborne duration to reduce false positives from slope transitions.
		/// Uses a two-phase landing: ground contact starts the transition, but the agent is not
		/// truly "landed" until GroundedAmount exceeds 0.9. During the transition, physics handles
		/// the descent with no snap or velocity zeroing.
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

				IsLanding = false;
			}
			else if (!wasGrounded)
			{
				// First frame of ground contact after being airborne.
				// Enter landing transition - don't fire event or snap yet.
				IsLanding = true;
			}

			// Check if landing transition completes (truly settled on ground).
			// Cannot complete while IsJumping - wait for vel.y < 0 to clear it first.
			if (IsLanding && !IsJumping && GroundedAmount > 0.9f)
			{
				// Truly landed. Fire event and apply landing effects.
				float surfaceAlignment = Mathf.Clamp01(Vector3.Dot(groundedHit.normal, rigidbodyWrapper.Up));
				float effectiveImpact = peakFallingSpeed * surfaceAlignment;

				// Scale impact by airborne duration.
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
				LandedEvent?.Invoke(effectiveImpact, groundedHit);
				peakFallingSpeed = 0f;
				airborneDuration = 0f;
				IsJumping = false;
				IsLanding = false;
			}

			wasGrounded = groundContact;
		}

		private void StepCheck()
		{
			if (IsJumping || !groundContact)
			{
				StepSupport = 0f;
				StepPoint = rigidbodyWrapper.Position + rigidbodyWrapper.Velocity.ClampMagnitude(stepHeight * 0.9f);
				return;
			}

			if (StepPoint == Vector3.zero)
			{
				StepPoint = rigidbodyWrapper.Position;
			}

			OptimizationSettings.Settings optimization = settings.Get(Entity.Priority);
			int rayCount = optimization.RayCount;

			// Map ground surface.
			Vector3 origin = rigidbodyWrapper.Position + SurfaceNormal * stepHeight;
			Vector2 radius = new Vector2(
				Mathf.Max(stepRadius.x, rigidbodyWrapper.RelativeVelocity.x * stepRadius.x * 0.5f),
				Mathf.Max(stepRadius.y, rigidbodyWrapper.RelativeVelocity.z * stepRadius.y * 0.5f));

			if (!PhysicsUtils.TubeCast(origin, radius, -SurfaceNormal, rigidbodyWrapper.Forward, stepHeight * 2f,
				layerMask, rayCount, out List<RaycastHit> hits, true, debug))
			{
				StepSupport = 0f;
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
				StepSupport = 0f;
				SurfaceNormal = Vector3.Slerp(SurfaceNormal, groundedHit.normal, normalSmoothing * Time.fixedDeltaTime);
				TerrainNormal = Vector3.Slerp(TerrainNormal, rigidbodyWrapper.Up, normalSmoothing * Time.fixedDeltaTime);
				StepPoint = rigidbodyWrapper.Position;
				return;
			}

			// Ensure outlier buffer is large enough.
			if (outlierBuffer == null || outlierBuffer.Length < validCount)
			{
				outlierBuffer = new float[validCount];
			}

			// --- Angle outlier filter (median-based) ---
			// Reject steep outlier hits whose slope angle vastly exceeds the median.
			// Median is used instead of mean so that a minority of extreme hits
			// (e.g. near-vertical wall hits beside cliffs) cannot drag the reference.
			for (int i = 0; i < validCount; i++)
			{
				outlierBuffer[i] = Vector3.Angle(rigidbodyWrapper.Up, hits[i].normal);
			}
			float medianAngle = MedianFloat(outlierBuffer, validCount);

			int filteredCount = 0;
			float maxAcceptedAngle = medianAngle + steepHitOutlierAngle;
			for (int i = 0; i < validCount; i++)
			{
				float hitAngle = Vector3.Angle(rigidbodyWrapper.Up, hits[i].normal);
				if (hitAngle <= maxAcceptedAngle)
				{
					if (i != filteredCount)
					{
						RaycastHit temp = hits[filteredCount];
						hits[filteredCount] = hits[i];
						hits[i] = temp;
					}
					filteredCount++;
				}
			}

			// Do not allow the filter to wipe out the sample entirely.
			if (filteredCount == 0)
			{
				filteredCount = validCount;
			}

			// --- Height outlier filter (median-based) ---
			// Reject hits whose elevation deviates too far from the median hit height.
			// This discards rays that punch through a ledge edge to the ground far below,
			// which would otherwise drag the step point and terrain average downward.
			if (outlierBuffer.Length < filteredCount)
			{
				outlierBuffer = new float[filteredCount];
			}
			for (int i = 0; i < filteredCount; i++)
			{
				outlierBuffer[i] = Vector3.Dot(hits[i].point, rigidbodyWrapper.Up);
			}
			float medianHeight = MedianFloat(outlierBuffer, filteredCount);

			int heightFilteredCount = 0;
			for (int i = 0; i < filteredCount; i++)
			{
				float hitHeight = Vector3.Dot(hits[i].point, rigidbodyWrapper.Up);
				if (Mathf.Abs(hitHeight - medianHeight) <= stepHeightOutlierDistance)
				{
					if (i != heightFilteredCount)
					{
						RaycastHit temp = hits[heightFilteredCount];
						hits[heightFilteredCount] = hits[i];
						hits[i] = temp;
					}
					heightFilteredCount++;
				}
			}

			// Do not allow the filter to wipe out the sample entirely.
			if (heightFilteredCount == 0)
			{
				heightFilteredCount = filteredCount;
			}

			filteredCount = heightFilteredCount;

			// Estimate footprint support from surviving filtered hits vs intended ray count.
			// This prevents a tiny rear sliver of valid hits from fully supporting the whole body at a ledge.
			StepSupport = rayCount > 0 ? (float)filteredCount / rayCount : 0f;
			StepSupport = Mathf.Clamp01(StepSupport);
			StepSupport = Mathf.InverseLerp(STEP_SUPPORT_MIN, STEP_SUPPORT_MAX, StepSupport);

			// Calculate surface normals from filtered hits only.
			if (stepNormals == null || stepNormals.Length != filteredCount)
			{
				stepNormals = new Vector3[filteredCount];
			}
			for (int i = 0; i < filteredCount; i++)
			{
				stepNormals[i] = hits[i].normal;
			}
			SurfaceNormal = Vector3.Slerp(SurfaceNormal, stepNormals.AverageDirection(), normalSmoothing * Time.fixedDeltaTime);

			// Calculate terrain points from filtered hits only.
			if (stepPoints == null || stepPoints.Length != filteredCount)
			{
				stepPoints = new Vector3[filteredCount];
			}
			for (int i = 0; i < filteredCount; i++)
			{
				stepPoints[i] = hits[i].point;
			}
			TerrainNormal = Vector3.Slerp(TerrainNormal, stepPoints.ApproxNormalFromPoints(rigidbodyWrapper.Up,
				out Vector3 center, debug, debugSize), normalSmoothing * Time.fixedDeltaTime);

			// Weaken the support center when only a small portion of the footprint is valid.
			Vector3 supportedCenter = Vector3.Lerp(rigidbodyWrapper.Position, center, StepSupport);

			// Calculate desired step-position.
			StepPoint = rigidbodyWrapper.Position +
				Vector3.Lerp(StepPoint - rigidbodyWrapper.Position,
				supportedCenter - rigidbodyWrapper.Position,
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
			float slopeDivergence = Mathf.Abs(SurfaceSlope - TerrainSlope);
			bool isComplexGeometry = slopeDivergence > stairDivergenceThreshold;

			if (!isSliding)
			{
				// Cannot enter sliding on complex geometry.
				if (!isComplexGeometry)
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

			if (Grounded && !IsJumping && rigidbodyWrapper.Velocity.y < flyThreshold)
			{
				bool suspendGroundCorrection = IsJumping || IsLanding;

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
				else if (!landedThisFrame && !wasSliding && !suspendGroundCorrection)
				{
					// Disperse along terrain normal.
					// Skipped on landing frame, slide-exit frame, and during ground transition.
					Vector3 movementDispersion = rigidbodyWrapper.Velocity.DisperseOnPlane(TerrainNormal) * Mobility;
					rigidbodyWrapper.AddForce(movementDispersion, ForceMode.VelocityChange);
				}

				if (!suspendGroundCorrection)
				{
					// Normal grounded state. Zero downward velocity and snap to ground.
					if (!isSliding)
					{
						Vector3 vel = rigidbodyWrapper.Velocity;
						if (vel.y < 0f)
						{
							rigidbodyWrapper.Velocity = new Vector3(vel.x, vel.y * (1f - GroundedAmount), vel.z);
						}
					}

					float targetY = StepPoint.y + Elevation;
					float snapWeight = GroundedAmount * StepSupport;
					float snappedY = Mathf.Lerp(rigidbodyWrapper.Position.y, targetY, snapWeight);
					if (!rigidbodyWrapper.IsKinematic)
					{
						rigidbodyWrapper.Position = rigidbodyWrapper.Position.SetY(snappedY);
					}
				}
				// During ground transition: no snap, no velocity zeroing.
				// Physics handles the descent until fully settled.

				// Apply partial gravity when not fully grounded (cliff edges, ledges, landing).
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

		private void CheckIfSafe()
		{
			if (Ground &&
				Grounded &&
				rigidbodyWrapper.Control.Value.Approx(1f) &&
				TerrainSlope < 0.33f &&
				!Sliding &&
				GroundedAmount.Approx(1f))
			{
				LastSafePosition = rigidbodyWrapper.Position;
			}
		}

		/// <summary>
		/// Initiates a jump by applying an impulse force.
		/// Direction should include both the surface normal and any input-based steering.
		/// </summary>
		/// <param name="force">Jump force vector (direction and magnitude).</param>
		public void Jump(Vector3 force)
		{
			// Convert force to velocity (F=ma -> v=F/m) so heavier agents jump lower.
			Vector3 jumpVelocity = force / rigidbodyWrapper.Mass;

			// Decompose into vertical and horizontal.
			// Vertical always applies fully. Horizontal goes through Push
			// so it won't exceed current sprint speed.
			Vector3 vertical = new Vector3(0f, jumpVelocity.y, 0f);
			Vector3 horizontal = new Vector3(jumpVelocity.x, 0f, jumpVelocity.z);

			// Zero negative vertical velocity before jumping so downhill slide speed
			// doesn't eat into the upward force.
			Vector3 vel = rigidbodyWrapper.Velocity;
			if (vel.y < 0f)
			{
				rigidbodyWrapper.Velocity = new Vector3(vel.x, 0f, vel.z);
			}

			rigidbodyWrapper.AddForce(vertical, ForceMode.VelocityChange);
			if (horizontal.sqrMagnitude > 0.01f)
			{
				rigidbodyWrapper.Push(horizontal);
			}

			IsJumping = true;
			IsLanding = true;
		}

		/// <summary>
		/// Tracks the jump-to-fall transition.
		/// IsJumping clears when vertical velocity goes negative (agent starts descending).
		/// </summary>
		private void UpdateJumpState()
		{
			if (!IsJumping)
			{
				return;
			}

			if (rigidbodyWrapper.Velocity.y < 0f)
			{
				IsJumping = false;
			}
		}

		/// <summary>
		/// Returns the median value from the first <paramref name="count"/> entries of the buffer.
		/// Sorts the range in-place using insertion sort (counts are small, typically <= 16).
		/// </summary>
		private static float MedianFloat(float[] buffer, int count)
		{
			// Insertion sort over the relevant range.
			for (int i = 1; i < count; i++)
			{
				float key = buffer[i];
				int j = i - 1;
				while (j >= 0 && buffer[j] > key)
				{
					buffer[j + 1] = buffer[j];
					j--;
				}
				buffer[j + 1] = key;
			}

			if (count % 2 == 1)
			{
				return buffer[count / 2];
			}
			return (buffer[count / 2 - 1] + buffer[count / 2]) * 0.5f;
		}
	}
}
