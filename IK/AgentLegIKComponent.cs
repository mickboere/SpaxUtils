using System;
using System.Collections.Generic;
using UnityEngine;
using RootMotion.FinalIK;

namespace SpaxUtils
{
	[DefaultExecutionOrder(901)]
	public class AgentLegIKComponent : EntityComponentMono
	{
		[Serializable]
		public class LegIK
		{
			public const float CLIP_THRESH = 0.66f;
			private const float MIN_SQR_MAG = 0.0001f;

			public Leg Leg => legs.Legs[legIndex];

			public Vector3 FootPositionOffset => Quaternion.Inverse(Leg.Sole.rotation) * (Leg.Foot.position - Leg.Sole.position);
			public Quaternion FootRotationOffset => Quaternion.Inverse(Leg.Sole.rotation) * Leg.Foot.rotation;

			public Quaternion TargetRotation
			{
				get
				{
					Vector3 normal = GetTargetNormal();
					return BuildGroundedRotation(normal);
				}
			}

			public Vector3 TargetPosition => Leg.TargetPoint + TargetRotation * FootPositionOffset + GetTargetNormal() * Leg.Elevation;
			public Vector3 HintPosition => GetAnimatedHintPosition();

			[SerializeField, ConstDropdown(typeof(IIKChainConstants))] private string ikChain;
			[SerializeField] private int legIndex;
			[SerializeField] private Transform hint;
			[SerializeField] private LayerMask clippingMask;
			[SerializeField] private float clippingRadius = 0.03f;
			[SerializeField, Range(0f, 90f)] private float maxClipNormalAngle = 60f;
			[SerializeField] private bool debug;

			private IIKComponent ikComponent;
			private GrounderComponent grounder;
			private AgentLegsComponent legs;
			private RigidbodyWrapper rigidbodyWrapper;

			private Vector3 lastGroundedForward;
			private Vector3 lastHintDirection;

			public void InjectDependencies(
				IIKComponent ikComponent,
				GrounderComponent grounder,
				AgentLegsComponent legs,
				RigidbodyWrapper wrapper)
			{
				this.ikComponent = ikComponent;
				this.grounder = grounder;
				this.legs = legs;
				this.rigidbodyWrapper = wrapper;
			}

			public void UpdateIK()
			{
				Leg.Clipping = false;

				Vector3 baseNormal = GetTargetNormal();
				Quaternion baseRotation = BuildGroundedRotation(baseNormal);

				Vector3 targetPosition = Leg.TargetPoint + baseRotation * FootPositionOffset + baseNormal * Leg.Elevation;
				Quaternion targetRotation = baseRotation * FootRotationOffset;
				float positionWeight = Leg.GroundedAmount * rigidbodyWrapper.Grip;
				float rotationWeight = positionWeight;

				if (grounder.Grounded &&
					(grounder.Sliding ||
						rigidbodyWrapper.Control < CLIP_THRESH ||
						rigidbodyWrapper.Grip < CLIP_THRESH ||
						Leg.GroundedAmount < CLIP_THRESH))
				{
					Vector3 kneePos = Leg.KneePos;
					Vector3 solePos = Leg.SolePos;
					Vector3 dir = solePos - kneePos;
					float dist = dir.magnitude;
					float castDistance = Mathf.Max(0f, dist + Leg.Elevation - clippingRadius);

					if (dist > 0f &&
						Physics.SphereCast(kneePos, clippingRadius, dir.normalized, out RaycastHit clipHit, castDistance, clippingMask, QueryTriggerInteraction.Ignore))
					{
						Leg.Clipping = true;
						Vector3 finalNormal = baseNormal;

						if (clipHit.normal != Vector3.zero)
						{
							float clipAngle = Vector3.Angle(clipHit.normal, rigidbodyWrapper.Up);
							if (clipAngle <= maxClipNormalAngle)
							{
								finalNormal = clipHit.normal;
								rotationWeight = 1f;
							}
						}

						Quaternion finalRotation = BuildGroundedRotation(finalNormal);

						targetPosition = clipHit.point + finalRotation * FootPositionOffset + finalNormal * Leg.Elevation;
						targetRotation = finalRotation * FootRotationOffset;
						positionWeight = 1f;

						if (debug)
						{
							Debug.DrawLine(kneePos, clipHit.point, Color.red);
							Debug.DrawRay(clipHit.point, clipHit.normal * 0.2f, Color.yellow);
							Debug.DrawRay(clipHit.point, finalNormal * 0.2f, Color.green);
							Debug.DrawRay(clipHit.point, GetProjectedAnimatedForward(finalNormal) * 0.2f, Color.cyan);
							SpaxDebug.Log(ikChain, "Clipping");
						}
					}
				}

				ikComponent.AddInfluencer(this, ikChain, 0, targetPosition, positionWeight, targetRotation, rotationWeight);

				if (hint != null)
				{
					hint.position = GetAnimatedHintPosition();
				}
			}

			public void DrawGizmos()
			{
				if (!debug || !Application.isPlaying)
				{
					return;
				}

				float scale = (Leg.Knee.position - Leg.Foot.position).magnitude * 0.15f;

				Gizmos.color = Color.magenta;
				Gizmos.DrawLine(Leg.Knee.position, GetAnimatedHintPosition());
				Gizmos.DrawSphere(GetAnimatedHintPosition(), scale * 0.5f);

				Gizmos.color = Color.black;
				Gizmos.DrawSphere(Leg.TargetPoint, scale * 0.5f);
				Gizmos.color = Color.red;
				Gizmos.DrawRay(Leg.TargetPoint, TargetRotation * Vector3.right * scale * 2f);
				Gizmos.color = Color.green;
				Gizmos.DrawRay(Leg.TargetPoint, TargetRotation * Vector3.up * scale * 2f);
				Gizmos.color = Color.blue;
				Gizmos.DrawRay(Leg.TargetPoint, TargetRotation * Vector3.forward * scale * 2f);
			}

			private Vector3 GetTargetNormal()
			{
				return Leg.ValidGround && Leg.GroundedHit.normal != Vector3.zero
					? Leg.GroundedHit.normal
					: Leg.Sole.up;
			}

			private Quaternion BuildGroundedRotation(Vector3 normal)
			{
				Vector3 groundedForward = GetProjectedAnimatedForward(normal);
				return Quaternion.LookRotation(groundedForward, normal);
			}

			private Vector3 GetProjectedAnimatedForward(Vector3 normal)
			{
				Vector3 groundedForward = Vector3.ProjectOnPlane(Leg.AnimatedSoleForward, normal);

				if (groundedForward.sqrMagnitude < MIN_SQR_MAG)
				{
					groundedForward = Vector3.ProjectOnPlane(Leg.Sole.forward, normal);
				}

				if (groundedForward.sqrMagnitude < MIN_SQR_MAG)
				{
					groundedForward = lastGroundedForward;
				}

				if (groundedForward.sqrMagnitude < MIN_SQR_MAG)
				{
					groundedForward = Vector3.ProjectOnPlane(Leg.AnimatedSoleForward, rigidbodyWrapper.Up);
				}

				groundedForward.Normalize();
				lastGroundedForward = groundedForward;
				return groundedForward;
			}

			private Vector3 GetAnimatedHintPosition()
			{
				Vector3 thighToFoot = Leg.Foot.position - Leg.Thigh.position;

				if (thighToFoot.sqrMagnitude < MIN_SQR_MAG)
				{
					return Leg.Knee.position + GetSeedHintDirection();
				}

				Vector3 bendDirection = Vector3.ProjectOnPlane(Leg.AnimatedKneeHintDirection, thighToFoot);

				if (bendDirection.sqrMagnitude < MIN_SQR_MAG)
				{
					if (lastHintDirection.sqrMagnitude < MIN_SQR_MAG)
					{
						lastHintDirection = GetSeedHintDirection();
					}

					return Leg.Knee.position + lastHintDirection * GetHintDistance();
				}

				bendDirection.Normalize();
				lastHintDirection = bendDirection;

				return Leg.Knee.position + bendDirection * GetHintDistance();
			}

			private float GetHintDistance()
			{
				float hintDistance = (Leg.Knee.position - Leg.Foot.position).magnitude;
				return Mathf.Max(hintDistance, 0.001f);
			}

			private Vector3 GetSeedHintDirection()
			{
				if (hint != null)
				{
					Vector3 thighToFoot = Leg.Foot.position - Leg.Thigh.position;
					Vector3 authoredDirection = hint.position - Leg.Knee.position;
					Vector3 projectedDirection = Vector3.ProjectOnPlane(authoredDirection, thighToFoot);

					if (projectedDirection.sqrMagnitude >= MIN_SQR_MAG)
					{
						return projectedDirection.normalized;
					}
				}

				if (lastHintDirection.sqrMagnitude >= MIN_SQR_MAG)
				{
					return lastHintDirection;
				}

				return Leg.AnimatedKneeHintDirection.normalized;
			}
		}

		[SerializeField] private List<LegIK> legs;

		private GrounderComponent grounderComponent;
		private GrounderFBBIK grounderFBBIK;

		public void InjectDependencies(IDependencyManager dependencyManager, GrounderComponent grounderComponent)
		{
			this.grounderComponent = grounderComponent;
			foreach (LegIK leg in legs)
			{
				dependencyManager.Inject(leg);
			}
		}

		protected void Awake()
		{
			grounderFBBIK = gameObject.GetComponentRelative<GrounderFBBIK>();
		}

		protected void Update()
		{
			if (grounderFBBIK != null)
			{
				grounderFBBIK.solver.heightOffset = grounderComponent.Elevation;
				grounderFBBIK.weight = grounderComponent.GroundedAmount > 0.9f ? 1f : 0f;
			}
		}

		protected void LateUpdate()
		{
			if (!isActiveAndEnabled)
			{
				return;
			}

			foreach (LegIK leg in legs)
			{
				leg.UpdateIK();
			}
		}

		protected void OnDrawGizmos()
		{
			if (!isActiveAndEnabled)
			{
				return;
			}

			foreach (LegIK leg in legs)
			{
				leg.DrawGizmos();
			}
		}
	}
}
