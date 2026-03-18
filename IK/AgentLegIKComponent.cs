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
			public Leg Leg => legs.Legs[legIndex];

			public Vector3 FootPositionOffset => Leg.Foot.position - Leg.Sole.position;
			public Quaternion FootRotationOffset => Quaternion.Inverse(Leg.Sole.rotation) * Leg.Foot.rotation;
			public Quaternion TargetRotation => !Leg.ValidGround || Leg.GroundedHit.normal == Vector3.zero ? Leg.Sole.rotation :
				Quaternion.LookRotation(Vector3.Cross(Leg.Sole.right, Leg.GroundedHit.normal), Leg.GroundedHit.normal);

			public Vector3 TargetPosition => Leg.TargetPoint + TargetRotation * FootPositionOffset + Leg.GroundedHit.normal * Leg.Elevation;
			public Vector3 HintPosition => Leg.Knee.position + Leg.Sole.forward;

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

				Vector3 baseNormal = Leg.ValidGround && Leg.GroundedHit.normal != Vector3.zero
					? Leg.GroundedHit.normal
					: Leg.Sole.up;

				Quaternion baseRotation = Quaternion.LookRotation(
					Vector3.Cross(Leg.Sole.right, baseNormal),
					baseNormal);

				Vector3 targetPosition = Leg.TargetPoint + baseRotation * FootPositionOffset + baseNormal * Leg.Elevation;
				Quaternion targetRotation = baseRotation * FootRotationOffset;
				float positionWeight = Leg.GroundedAmount * rigidbodyWrapper.Grip;
				float rotationWeight = positionWeight;

				if (grounder.Grounded && (grounder.Sliding || rigidbodyWrapper.Control < 0.66f || rigidbodyWrapper.Grip < 0.66f || Leg.GroundedAmount < 0.66f))
				{
					Leg.Clipping = true;
					Vector3 dir = Leg.SolePos - Leg.KneePos;
					float dist = dir.magnitude;

					if (dist > 0.0001f &&
						Physics.SphereCast(Leg.KneePos, clippingRadius, dir.normalized, out RaycastHit clipHit, dist + Leg.Elevation, clippingMask, QueryTriggerInteraction.Ignore))
					{
						Vector3 finalNormal = baseNormal;

						if (clipHit.normal != Vector3.zero)
						{
							float clipAngle = Vector3.Angle(clipHit.normal, rigidbodyWrapper.Up);
							if (clipAngle <= maxClipNormalAngle)
							{
								finalNormal = clipHit.normal;
							}
						}

						Quaternion finalRotation = Quaternion.LookRotation(
							Vector3.Cross(Leg.Sole.right, finalNormal),
							finalNormal);

						targetPosition = clipHit.point + finalRotation * FootPositionOffset + finalNormal * Leg.Elevation;
						targetRotation = finalRotation * FootRotationOffset;
						positionWeight = 1f;
						rotationWeight = 1f;

						if (debug)
						{
							Debug.DrawLine(Leg.KneePos, clipHit.point, Color.red);
							Debug.DrawRay(clipHit.point, clipHit.normal * 0.2f, Color.yellow);
							Debug.DrawRay(clipHit.point, finalNormal * 0.2f, Color.green);
						}
					}
				}

				ikComponent.AddInfluencer(this, ikChain, 0, targetPosition, positionWeight, targetRotation, rotationWeight);
				hint.position = HintPosition;
			}

			public void DrawGizmos()
			{
				if (!debug || !Application.isPlaying)
				{
					return;
				}

				float scale = (Leg.Knee.position - Leg.Foot.position).magnitude * 0.15f;

				// Knee Hint
				Gizmos.color = Color.magenta;
				Gizmos.DrawLine(Leg.Knee.position, HintPosition);
				Gizmos.DrawSphere(HintPosition, scale * 0.5f);

				// Target position and orientation.
				Gizmos.color = Color.black;
				Gizmos.DrawSphere(Leg.TargetPoint, scale * 0.5f);
				Gizmos.color = Color.red;
				Gizmos.DrawRay(Leg.TargetPoint, TargetRotation * Vector3.right * scale * 2f);
				Gizmos.color = Color.green;
				Gizmos.DrawRay(Leg.TargetPoint, TargetRotation * Vector3.up * scale * 2f);
				Gizmos.color = Color.blue;
				Gizmos.DrawRay(Leg.TargetPoint, TargetRotation * Vector3.forward * scale * 2f);
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
