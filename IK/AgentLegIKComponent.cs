using System;
using System.Collections.Generic;
using UnityEngine;
using RootMotion.FinalIK;

namespace SpaxUtils
{
	//[DefaultExecutionOrder(9000)]
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
			[SerializeField] private float clipThreshold = 0.03f;
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
				float weight = Leg.GroundedAmount * rigidbodyWrapper.Grip;

				if (grounder.Grounded && (grounder.Sliding || rigidbodyWrapper.Control < 0.5f || weight < 0.9f))
				{
					// Prevent feet clipping through ground.
					Vector3 dir = Leg.SolePos - Leg.KneePos;
					if (Physics.Raycast(Leg.KneePos, dir, out RaycastHit clipHit, dir.magnitude - clipThreshold, clippingMask))
					{
						Leg.UpdateGround(true, 1f, true, clipHit.point, clipHit);
						weight = 1f;
						if (debug)
						{
							Debug.DrawRay(Leg.KneePos, dir, Color.red);
							SpaxDebug.Log($"Clip Leg[{legIndex}]!");
						}
					}
				}

				ikComponent.AddInfluencer(this, ikChain, 0, TargetPosition, weight, TargetRotation * FootRotationOffset, weight);
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

			if (grounderFBBIK != null)
			{
				grounderFBBIK.solver.heightOffset = grounderComponent.Elevation;
				grounderFBBIK.weight = grounderComponent.Grounded ? 1f : 0f;
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
