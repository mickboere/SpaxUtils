using SpaxUtils;
using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	public class LegacyGrounderComponent : EntityComponentBase//, IAgentGrounderComponent
	{
		private const float CENTIMETER = 0.01f;

		public event Action<bool> GroundedStateChangedEvent;

		public Transform Transform { get; private set; }
		public ColliderWrapper FeetCollider => feetCollider;
		public RaycastHit? GroundedHit { get; private set; }
		public bool Grounded => GroundedHit.HasValue;

		/// <summary>
		/// The average ground normal around the player.
		/// </summary>
		public Vector3 GroundNormal { get; private set; }
		public float GroundSlopeAngle => Grounded ? Vector3.Angle(Transform.up, GroundNormal) : 0f;
		public bool ExceedingMaxSlope => GroundSlopeAngle > maxSlopeAngle;
		public float Traction => ExceedingMaxSlope ? 0f : CalculateTraction(GroundSlopeAngle);
		public bool Slipping => Grounded && Traction <= slippingTractionTreshold && rigidbodyWrapper.Velocity.y < -slippingSpeedTreshold;

		public Vector3 CurrentStepPoint { get; private set; }
		public float CurrentStepHeight => CurrentStepPoint.y - Transform.position.y;
		public float CurrentStepDistance => (CurrentStepPoint - Transform.position).FlattenY().magnitude;

		[Header("Settings")]
		[SerializeField] private ColliderWrapper feetCollider;
		[SerializeField] private LayerMask groundedLayerMask = ~0;
		[SerializeField] private float groundedReach = 0.5f;
		[SerializeField] private int groundNormalChecks = 50;

		[Header("Stepping")]
		[SerializeField] private float stepHeight = 0.5f;
		[SerializeField] private int stepChecksDepth = 30;
		[SerializeField] private int stepChecksWidth = 30;
		[SerializeField] private float stepCheckDistance = 0.7f;

		[Header("Friction")]
		[SerializeField] private float minStaticFriction = 0.2f;
		[SerializeField] private float defaultStaticFriction = 3f;
		[SerializeField] private float minDynamicFriction = 0.2f;
		[SerializeField] private float defaultDynamicFriction = 3f;
		[SerializeField] private float maxSlopeAngle = 60f;
		[SerializeField] private AnimationCurve tractionCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[SerializeField] private float slippingSpeedTreshold = 4f;
		[SerializeField] private float slippingTractionTreshold = 0.1f;

		[Header("Optional")]
		[SerializeField] private GrounderFBBIK grounderIk;
		[SerializeField] private float grounderIkInterpolationSpeed = 10f;

		[Header("Debug")]
		[SerializeField] private bool drawGizmos = true;
		[SerializeField] private Mesh groundNormalMesh;
		[SerializeField] private Vector3 groundNormalMeshScale = Vector3.one * 0.1f;

		private PhysicMaterial physicMaterial;
		private RigidbodyWrapper rigidbodyWrapper;

		public void InjectDependencies(IEntity entity, RigidbodyWrapper rigidbodyWrapper, ColliderWrapper colliderWrapper)
		{
			Transform = entity.GameObject.transform;
			this.rigidbodyWrapper = rigidbodyWrapper;
			if (feetCollider == null)
			{
				feetCollider = colliderWrapper;
			}
		}

		protected void Start()
		{
			SetUpPhysicMaterial();
		}

		protected void FixedUpdate()
		{
			UpdateGroundedState();
		}

		/// <summary>
		/// Called in FixedUpdate.
		/// </summary>
		private void UpdateGroundedState()
		{
			bool wasGrounded = Grounded;
			RaycastHit[] sphereCastHits = Physics.SphereCastAll(feetCollider.Position, feetCollider.Radius - CENTIMETER, Vector3.down, groundedReach, groundedLayerMask);
			if (sphereCastHits.Length > 0)
			{
				GroundedHit = sphereCastHits.OrderBy((x) => x.distance).First();
				GetGroundNormal();
				GetSteppingData();
			}
			else
			{
				GroundedHit = null;
			}

			if (wasGrounded != Grounded)
			{
				GroundedStateChangedEvent?.Invoke(Grounded);
			}

			// Update friction values by interpolating from min to default with the evaluated traction.
			physicMaterial.staticFriction = Mathf.Clamp01(Mathf.Lerp(minStaticFriction, defaultStaticFriction, Traction));
			physicMaterial.dynamicFriction = Mathf.Clamp01(Mathf.Lerp(minDynamicFriction, defaultDynamicFriction, Traction));

			if (grounderIk != null)
			{
				grounderIk.weight = Mathf.Lerp(grounderIk.weight, Grounded ? 1f : 0f, grounderIkInterpolationSpeed * Time.fixedDeltaTime);
			}
		}

		private void GetGroundNormal()
		{
			// We perform a cylindrical array of raycasts to get the ground normal because the spherecast tends to returns odd normals.
			int hits = 0;
			Vector3 averageNormal = Vector3.zero;
			for (int i = 0; i < groundNormalChecks; i++)
			{
				float l = Mathf.PI * 2f / groundNormalChecks * i;
				Vector3 origin = feetCollider.Position + new Vector3(Mathf.Cos(l) * feetCollider.Radius * 0.5f, 0f, Mathf.Sin(l) * feetCollider.Radius * 0.5f);
				if (Physics.Raycast(origin, -Transform.up, out RaycastHit hit, groundedReach + feetCollider.Radius, groundedLayerMask))
				{
					hits++;
					averageNormal += hit.normal;
				}
			}

			if (hits == 0)
			{
				GroundNormal = Transform.up;
			}
			else
			{
				GroundNormal = averageNormal / hits;
			}
		}

		private void GetSteppingData()
		{
			CurrentStepPoint = Transform.position;
			float steepest = 0f;
			for (int i = 0; i < stepChecksWidth; i++)
			{
				float l = Mathf.PI * 2f / (stepChecksWidth * 2) * i;
				Vector3 direction = Transform.rotation * Vector3.Normalize(new Vector3(Mathf.Cos(l), 0f, Mathf.Sin(l)));
				for (int j = 0; j < stepChecksDepth; j++)
				{
					Vector3 rayOrigin = Transform.position + Vector3.up * stepHeight + direction * (feetCollider.Radius - CENTIMETER + (stepCheckDistance / stepChecksDepth * j));
					if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, stepHeight - CENTIMETER, groundedLayerMask))
					{
						Vector3 point = hit.point;
						point.y = Transform.position.y + (point.y - Transform.position.y) * CalculateTraction(Vector3.Angle(hit.normal, Transform.up));
						float angle = Vector3.Angle(point.FlattenY() - Transform.position.FlattenY(), point - Transform.position);
						if (angle > steepest)
						{
							CurrentStepPoint = point;
							steepest = angle;
						}
					}
				}
			}
		}

		private float CalculateTraction(float slopeAngle)
		{
			return tractionCurve.Evaluate(1f - Mathf.Clamp01(slopeAngle / maxSlopeAngle));
		}

		protected void OnDrawGizmos()
		{
			if (Application.isPlaying && drawGizmos)
			{
				if (Grounded)
				{
					Gizmos.color = new Color(0.5f, 1f, 0.5f, 0.2f); // Collider-Green
					Gizmos.DrawWireSphere(feetCollider.Position, feetCollider.Radius - CENTIMETER);
					Gizmos.color = new Color(0.5f, 1f, 0.5f, 0.5f); // Collider-Green
					Gizmos.DrawWireSphere(feetCollider.Position - Transform.up * GroundedHit.Value.distance, feetCollider.Radius - CENTIMETER);
				}
				else
				{
					Gizmos.color = new Color(1f, 0.5f, 0.5f, 0.2f); // Brite Red
					Gizmos.DrawWireSphere(feetCollider.Position - Transform.up * groundedReach, feetCollider.Radius - CENTIMETER);
				}

				if (Grounded)
				{
					// Ground Normal Plane
					if (groundNormalMesh != null)
					{
						// World normal
						Gizmos.color = new Color(.5f, .5f, .5f, 0.1f); // Grey
						Gizmos.DrawWireMesh(groundNormalMesh, -1, Transform.position, Quaternion.LookRotation(Transform.forward), groundNormalMeshScale);

						// Ground normal
						Gizmos.color = new Color(0f, 1f, 1f, 0.1f); // Cyan
						Gizmos.DrawWireMesh(groundNormalMesh, -1, Transform.position, Quaternion.LookRotation(Vector3.Cross(GroundNormal, Transform.right)), groundNormalMeshScale);
					}

					// Stepping
					Gizmos.color = new Color(.5f, .5f, .1f, 0.4f); // Brown
					for (int i = 0; i < stepChecksWidth; i++)
					{
						float l = Mathf.PI * 2f / (stepChecksWidth * 2) * i;
						Vector3 direction = Transform.rotation * Vector3.Normalize(new Vector3(Mathf.Cos(l), 0f, Mathf.Sin(l)));
						for (int j = 0; j < stepChecksDepth; j++)
						{
							Vector3 rayOrigin = Transform.position + Vector3.up * stepHeight + direction * (feetCollider.Radius + (stepCheckDistance / stepChecksDepth * j));
							if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, stepHeight - CENTIMETER, groundedLayerMask))
							{
								Gizmos.DrawLine(rayOrigin, hit.point);
							}
						}
					}
					Gizmos.color = new Color(0.6f, 0.6f, 0.1f, 1f); // Brownish
					Gizmos.DrawLine(Transform.position, CurrentStepPoint);
					Gizmos.color = Color.magenta;
					Vector3 stepStart = Transform.position + (CurrentStepPoint - Transform.position).FlattenY();
					Gizmos.DrawLine(Transform.position, stepStart);
					Gizmos.DrawLine(stepStart, stepStart + Vector3.up * CurrentStepHeight);

					// Ground Normal Rays
					Gizmos.color = new Color(0.8f, 0.5f, 1f, 0.5f); // Purple
					for (int i = 0; i < groundNormalChecks; i++)
					{
						float l = Mathf.PI * 2f / groundNormalChecks * i;
						Vector3 origin = feetCollider.Position + new Vector3(Mathf.Sin(l) * feetCollider.Radius, 0f, Mathf.Cos(l) * feetCollider.Radius);
						if (Physics.Raycast(origin, -Transform.up, out RaycastHit hit, groundedReach + feetCollider.Radius, groundedLayerMask))
						{
							Gizmos.DrawLine(origin, hit.point);
						}
					}

					// SphereCast hit point
					Gizmos.color = Color.red;
					Gizmos.DrawSphere(GroundedHit.Value.point, 0.025f);
				}
			}
		}

		private void SetUpPhysicMaterial()
		{
			physicMaterial = new PhysicMaterial();
			physicMaterial.staticFriction = defaultStaticFriction;
			physicMaterial.dynamicFriction = defaultDynamicFriction;
			physicMaterial.frictionCombine = PhysicMaterialCombine.Average;
			physicMaterial.bounciness = 0f;
			physicMaterial.bounceCombine = PhysicMaterialCombine.Minimum;
			feetCollider.Collider.material = physicMaterial;
		}
	}
}
