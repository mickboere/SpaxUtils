using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	public class BaseProjectile : MonoBehaviour, IProjectile
	{
		public Vector3 Point => transform.position;
		public float Radius => radius;
		public Vector3 StartPoint { get; private set; }
		public float Range { get { return range; } set { range = value; } }
		public float Speed => speed * startupTimer.Progress;
		public Vector3 Velocity => transform.forward * Speed;

		public IAgent Source => agent;
		public ITargetable Target => target;

		[Header("Projectile")]
		[SerializeField] private float radius = 0.2f;
		[SerializeField] private float range = 30f;
		[SerializeField] private float speed = 10f;
		[SerializeField] private float mass = 1f;
		[SerializeField] private float duration = 6f;
		[SerializeField] private float startupTime = 0f;
		[Header("Hit Detection")]
		[SerializeField] private LayerMask layerMask;
		[SerializeField] private bool destroyOnHit;
		[SerializeField] private float repeatHitTime = 0.5f;
		[SerializeField] private GameObject instantiateOnHit;
		[Header("Destruction")]
		[SerializeField] private GameObject[] detachOnDestroy;

		private ProjectileService projectileService;

		protected IAgent agent;
		protected ITargetable target;

		private TimerStruct lifeTimer;
		private TimerStruct startupTimer;
		private Dictionary<Transform, TimerStruct> hitTimers = new Dictionary<Transform, TimerStruct>();

		public void InjectDependencies(ProjectileService projectileService, IAgent agent)
		{
			this.projectileService = projectileService;
			this.agent = agent;
			target = agent.Targeter.Target;
		}

		protected void Awake()
		{
			projectileService.Add(this);
			StartPoint = Point;
			lifeTimer = new TimerStruct(duration);
			startupTimer = new TimerStruct(startupTime);
		}

		protected void Update()
		{
			bool detectedHit = false;
			if (DetectHits(Time.deltaTime, out RaycastHit[] hits))
			{
				detectedHit = true;
				if (destroyOnHit)
				{
					// Only process closest hit.
					ProcessHit(hits.OrderBy(h => h.distance).First());
				}
				else
				{
					// Process all hits.
					foreach (RaycastHit hit in hits)
					{
						ProcessHit(hit);
					}
				}
			}

			if (lifeTimer.Expired || (detectedHit && destroyOnHit) || StartPoint.Distance(Point) > Range)
			{
				Destroy(gameObject);
			}
			else
			{
				OnUpdate(Time.deltaTime);
			}
		}

		/// <inheritdoc/>
		public bool IsInPath(Vector3 point, float radius, float delta, out Vector3 closest, out float distance)
		{
			// Project the projectile's position forward in time and do an overlapping sphere check to
			// the closest point on that projection to see whether the point falls on the projectile's path.
			closest = point.ClosestOnLine(Point, Point + Velocity * delta);
			distance = point.Distance(closest) - (Radius + radius);
			return distance <= 0f;
		}

		protected void OnDestroy()
		{
			projectileService.Remove(this);
			foreach (GameObject go in detachOnDestroy)
			{
				go.transform.SetParent(null);
			}
		}

		protected virtual bool DetectHits(float delta, out RaycastHit[] hits)
		{
			hits = Physics.SphereCastAll(Point, radius, Velocity, Speed * delta, layerMask).Where((h) => h.transform.root != agent.Transform.root).ToArray();
			return hits.Length > 0;
		}

		protected virtual void ProcessHit(RaycastHit hit)
		{
			Transform root = hit.transform.root;
			if (hitTimers.ContainsKey(root) && !hitTimers[root].Expired)
			{
				return;
			}

			hitTimers[root] = new TimerStruct(repeatHitTime);

			if (instantiateOnHit != null)
			{
				// Instantiate hit effect.
				Instantiate(instantiateOnHit, hit.point, Quaternion.LookRotation(-Velocity));
			}

			if (hit.transform.gameObject.TryGetComponentRelative(out IHittable hittable))
			{
				// An IHittable was hit, generate HitData and send it.
				HitData hitData = new HitData(
					hittable,
					agent,
					hit.point,
					mass, // Inertia mass transfer
					Velocity,
					Velocity.normalized,
					mass // Stun force transfer
				);

				if (hittable.Hit(hitData))
				{
					// Do stuff.
					OnHit(hitData);
				}
			}
		}

		protected virtual void OnUpdate(float delta) { }

		protected virtual void OnHit(HitData hitData) { }
	}
}
