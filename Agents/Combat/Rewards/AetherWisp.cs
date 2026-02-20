using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Pooled wisp that drifts with layered noise and gradually homes in on the reservation target.
	/// Claims once on arrival (or timeout), and only reports Finished once trail/particles have dissipated.
	/// </summary>
	public class AetherWisp : PooledItemBase
	{
		public override bool Finished => finished;
		public override int DefaultPoolSize => defaultPoolSize;

		[SerializeField, Tooltip("Default pool size for this pooled item.")]
		private int defaultPoolSize = 10;

		[Header("Movement")]
		[SerializeField, Tooltip("Base travel speed in units per second.")]
		private float speed = 12f;

		[SerializeField, Tooltip("How quickly the wisp turns toward its desired direction. Higher = more homing, less drifting.")]
		private float turnRate = 6f;

		[SerializeField, Tooltip("Distance (in units) from the target at which the wisp is considered to have arrived and will claim the reservation.")]
		private float arriveDistance = 0.5f;

		[Header("Drift - Wander (low freq, high strength)")]
		[SerializeField, Tooltip("Large, slow drift that makes the wisp 'wander' off course before suction pulls it in.")]
		private float wanderStrength = 2.5f;

		[SerializeField, Tooltip("Frequency of the wander drift. Lower = slower, broader wandering motion.")]
		private float wanderFrequency = 0.35f;

		[Header("Drift - Flutter (high freq, low strength)")]
		[SerializeField, Tooltip("Small, faster drift that adds airy jitter on top of the big wander motion.")]
		private float flutterStrength = 0.6f;

		[SerializeField, Tooltip("Frequency of the flutter drift. Higher = quicker, more 'fluttery' motion.")]
		private float flutterFrequency = 1.6f;

		[Header("Suction")]
		[SerializeField, Tooltip("Radius around the target where the wisp becomes more attracted: drift is reduced and turning is increased.")]
		private float suctionRadius = 10f;

		[SerializeField, Tooltip("Multiplier applied to Turn Rate while inside Suction Radius. Higher = harder to dodge once inside the radius.")]
		private float suctionTurnRateMultiplier = 2.5f;

		[SerializeField, Tooltip("Multiplier applied to total drift while inside Suction Radius. Lower = less random near the target.")]
		private float suctionDriftMultiplier = 0.25f;

		[Header("Soft Avoidance")]
		[SerializeField, Tooltip("Layers that the wisp will try to steer away from. It will still pass through colliders; this is only a steering bias.")]
		private LayerMask avoidanceMask = ~0;

		[SerializeField, Tooltip("How far ahead (in units) to probe for nearby colliders along the current desired travel direction.")]
		private float avoidanceProbeDistance = 1.2f;

		[SerializeField, Tooltip("Radius of the probe sphere cast. Larger values make the wisp steer away sooner from surfaces.")]
		private float avoidanceProbeRadius = 0.25f;

		[SerializeField, Tooltip("How strongly the wisp biases its direction away from a detected surface. 0 = off, 1 = strong steering.")]
		private float avoidanceStrength = 0.9f;

		[Header("Failsafe")]
		[SerializeField, Tooltip("Maximum lifetime in seconds. If exceeded, the reservation is claimed immediately even if not arrived.")]
		private float maxDuration = 8.0f;

		[Header("Visual Completion")]
		[SerializeField, Tooltip("TrailRenderer used for the wisp. Finished won't be true until the trail has fully dissipated.")]
		private TrailRenderer trail;

		[SerializeField, Tooltip("ParticleSystem used for the wisp. Finished won't be true until particles are no longer alive.")]
		private ParticleSystem particles;

		private AetherRewardService rewardService;
		private AetherRewardService.AetherReservation reservation;

		private Vector3 velocity;
		private bool claimed;
		private bool finishing;
		private bool finished;

		private float noiseSeed;
		private float ageSeconds;

		public void InjectDependencies(AetherRewardService rewardService)
		{
			this.rewardService = rewardService;
		}

		public void Initialize(AetherRewardService.AetherReservation reservation)
		{
			this.reservation = reservation;

			velocity = Vector3.zero;
			claimed = false;
			finishing = false;
			finished = false;

			noiseSeed = Random.value * 1000f;
			ageSeconds = 0f;

			// Reset visuals for reuse.
			if (trail != null)
			{
				trail.Clear();
				trail.emitting = true;
			}

			if (particles != null)
			{
				particles.Clear(true);
				particles.Play(true);
			}
		}

		private void Awake()
		{
			if (trail == null)
			{
				trail = GetComponentInChildren<TrailRenderer>(true);
			}

			if (particles == null)
			{
				particles = GetComponentInChildren<ParticleSystem>(true);
			}
		}

		private void Update()
		{
			if (finished)
			{
				return;
			}

			if (finishing)
			{
				TryCompleteAfterVisuals();
				return;
			}

			ageSeconds += Time.deltaTime;
			if (maxDuration > 0f && ageSeconds >= maxDuration)
			{
				StartFinishing(claim: true);
				return;
			}

			if (reservation == null || reservation.Target == null || reservation.Target.Targetable == null)
			{
				StartFinishing(claim: false);
				return;
			}

			Vector3 targetPos = reservation.Target.Targetable.Center;
			Vector3 pos = transform.position;

			Vector3 toTarget = targetPos - pos;
			float dist = toTarget.magnitude;

			if (dist <= arriveDistance)
			{
				StartFinishing(claim: true);
				return;
			}

			Vector3 dirToTarget = dist > 0.0001f ? (toTarget / dist) : Vector3.forward;

			// Suction: inside suctionRadius, reduce drift and increase turning toward the target.
			float driftMul = 1.0f;
			float turnMul = 1.0f;
			if (suctionRadius > 0.0001f && dist <= suctionRadius)
			{
				float t = Mathf.InverseLerp(suctionRadius, arriveDistance, dist);
				driftMul = Mathf.Lerp(1.0f, suctionDriftMultiplier, t);
				turnMul = Mathf.Lerp(1.0f, suctionTurnRateMultiplier, t);
			}

			Vector3 drift =
				SampleDriftOctave(pos, Time.time, wanderFrequency) * wanderStrength +
				SampleDriftOctave(pos, Time.time, flutterFrequency) * flutterStrength;

			Vector3 desiredDir = (dirToTarget + (drift * driftMul)).normalized;

			// Soft avoidance: steer away from nearby colliders without preventing traversal.
			desiredDir = ApplySoftAvoidance(pos, desiredDir);

			// Gradual homing: smoothly steer velocity toward desiredDir.
			float dt = Time.deltaTime;
			float steer = 1.0f - Mathf.Exp(-Mathf.Max(0.0f, turnRate * turnMul) * dt);

			Vector3 desiredVel = desiredDir * speed;
			velocity = Vector3.Lerp(velocity, desiredVel, steer);

			transform.position = pos + (velocity * dt);
		}

		private Vector3 ApplySoftAvoidance(Vector3 pos, Vector3 desiredDir)
		{
			if (avoidanceStrength <= 0f || avoidanceProbeDistance <= 0f || avoidanceProbeRadius <= 0f)
			{
				return desiredDir;
			}

			// Probe forward in the desired direction. If we would hit something soon, bias away from it.
			if (Physics.SphereCast(pos, avoidanceProbeRadius, desiredDir, out RaycastHit hit, avoidanceProbeDistance, avoidanceMask, QueryTriggerInteraction.Ignore))
			{
				Vector3 away = hit.normal;
				if (away.sqrMagnitude > 0.0001f)
				{
					away.Normalize();
					return Vector3.Slerp(desiredDir, (desiredDir + away).normalized, Mathf.Clamp01(avoidanceStrength));
				}
			}

			return desiredDir;
		}

		private Vector3 SampleDriftOctave(Vector3 pos, float time, float frequency)
		{
			float t = (time + noiseSeed) * frequency;

			float nx = (Mathf.PerlinNoise(pos.y + t, pos.z + 0.17f + t) * 2.0f) - 1.0f;
			float ny = (Mathf.PerlinNoise(pos.z + t, pos.x + 0.33f + t) * 2.0f) - 1.0f;
			float nz = (Mathf.PerlinNoise(pos.x + t, pos.y + 0.51f + t) * 2.0f) - 1.0f;

			Vector3 n = new Vector3(nx, ny, nz);

			// Bias toward sideways/up so homing remains readable.
			n *= 0.7f;
			n.y *= 1.2f;

			return n;
		}

		private void StartFinishing(bool claim)
		{
			if (finishing || finished)
			{
				return;
			}

			if (claim && !claimed && reservation != null && rewardService != null)
			{
				rewardService.Claim(reservation.ReservationId);
				claimed = true;
			}

			reservation = null;
			finishing = true;

			if (trail != null)
			{
				trail.emitting = false;
			}

			if (particles != null)
			{
				particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
			}

			TryCompleteAfterVisuals();
		}

		private void TryCompleteAfterVisuals()
		{
			bool trailDone = true;
			if (trail != null)
			{
				trailDone = !trail.emitting && trail.positionCount == 0;
			}

			bool particlesDone = true;
			if (particles != null)
			{
				particlesDone = !particles.IsAlive(true);
			}

			if (trailDone && particlesDone)
			{
				finished = true;
			}
		}
	}
}
