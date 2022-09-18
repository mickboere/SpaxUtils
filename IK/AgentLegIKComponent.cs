using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	//[DefaultExecutionOrder(9000)]
	public class AgentLegIKComponent : EntityComponentBase
	{
		[Serializable]
		public class LegIK
		{
			public ILeg Leg => legs.Legs[legIndex];

			public Vector3 FootPositionOffset => Leg.Foot.position - footHelper.position;
			public Quaternion FootRotationOffset => Quaternion.Inverse(footHelper.rotation) * Leg.Foot.rotation;
			public Quaternion TargetRotation => Leg.GroundedHit.normal == Vector3.zero ? Quaternion.identity :
				Quaternion.LookRotation(Vector3.Cross(footHelper.right, Leg.GroundedHit.normal), Leg.GroundedHit.normal);

			public Vector3 TargetPosition => Leg.TargetPoint + TargetRotation * FootPositionOffset;
			public Vector3 HintPosition => Leg.Knee.position + footHelper.forward;

			[SerializeField, ConstDropdown(typeof(IIKChainConstants))] private string ikChain;
			[SerializeField] private int legIndex;
			[SerializeField] private Transform footHelper;
			[SerializeField] private Transform hint;
			[SerializeField] private float clipThreshold = 0.025f;
			[SerializeField] private bool debug;

			private IIKComponent ikComponent;
			private IGrounderComponent grounder;
			private ILegsComponent legs;
			private RigidbodyWrapper rigidbodyWrapper;

			public void InjectDependencies(
				IIKComponent ikComponent,
				IGrounderComponent grounder,
				ILegsComponent legs,
				RigidbodyWrapper wrapper)
			{
				this.ikComponent = ikComponent;
				this.grounder = grounder;
				this.legs = legs;
				this.rigidbodyWrapper = wrapper;
			}

			public void UpdateIK(LayerMask layerMask)
			{
				float weight = 0f;

				if (!rigidbodyWrapper.HasControl || grounder.Sliding)
				{
					Vector3 dir = footHelper.position - Leg.Knee.position;
					float length = dir.magnitude;
					if (Physics.Raycast(Leg.Knee.position, dir, out RaycastHit hit, length, layerMask) &&
						length - hit.distance > clipThreshold)
					{
						weight = 1f;
						Leg.UpdateFoot(false, 1f, true, hit.point, hit);
					}
				}
				else if (grounder.Grounded)
				{
					float movement = Mathf.Clamp01(rigidbodyWrapper.Speed * 10f).InOutSine();
					weight = Leg.GroundedAmount * rigidbodyWrapper.Grip * movement;
				}

				//if (debug)
				//{
				//	SpaxDebug.Log($"IK {ikChain}", $"weight{weight:N2}, ground{Leg.GroundedAmount:N2}, grip{wrapper.Grip:N2}, move{Mathf.Clamp01(wrapper.Speed * 10f).InOutSine():N2}");
				//}

				ikComponent.AddInfluencer(this, ikChain, TargetPosition, weight, TargetRotation * FootRotationOffset, weight);
				hint.position = HintPosition;
			}

			public void DrawGizmos()
			{
				if (!debug || !Application.isPlaying)
				{
					return;
				}

				float scale = (Leg.Knee.position - Leg.Foot.position).magnitude * 0.05f;

				// Knee Hint
				Gizmos.color = Color.magenta;
				Gizmos.DrawLine(Leg.Knee.position, HintPosition);
				Gizmos.DrawSphere(HintPosition, 0.05f);

				// Target position and orientation.
				Gizmos.color = Color.black;
				Gizmos.DrawSphere(Leg.TargetPoint, scale);
				Gizmos.color = Color.red;
				Gizmos.DrawRay(TargetPosition, TargetRotation * Vector3.right * scale * 2f);
				Gizmos.color = Color.green;
				Gizmos.DrawRay(TargetPosition, TargetRotation * Vector3.up * scale * 2f);
				Gizmos.color = Color.blue;
				Gizmos.DrawRay(TargetPosition, TargetRotation * Vector3.forward * scale * 2f);
			}
		}

		[SerializeField] private LayerMask layerMask;
		[SerializeField] private List<LegIK> legs;

		public void InjectDependencies(IDependencyManager dependencyManager)
		{
			foreach (LegIK leg in legs)
			{
				dependencyManager.Inject(leg);
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
				leg.UpdateIK(layerMask);
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
