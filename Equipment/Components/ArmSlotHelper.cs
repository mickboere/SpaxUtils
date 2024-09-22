using System;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	public class ArmSlotHelper : IDisposable
	{
		public bool IsLeft { get; }

		private string IKChain => IsLeft ? IKChainConstants.LEFT_ARM : IKChainConstants.RIGHT_ARM;
		private float ElbowHintWeight
		{
			get { return IsLeft ? finalIKComponent.LeftElbowHintWeight : finalIKComponent.RightElbowHintWeight; }
			set { if (IsLeft) { finalIKComponent.LeftElbowHintWeight = value; } else { finalIKComponent.RightElbowHintWeight = value; } }
		}

		private int prio;
		private IAgent agent;
		private AgentArmsComponent component;
		private IIKComponent ik;
		private TransformLookup lookup;
		private RigidbodyWrapper rigidbodyWrapper;
		private FinalIKComponent finalIKComponent;

		private Transform hand;
		private Vector3 targetPosSmooth;
		private Vector3 posVelocity;

		private Quaternion targetRotationSmooth;
		private Quaternion rotVelocity;

		public ArmSlotHelper(bool isLeft, int prio)
		{
			IsLeft = isLeft;
			this.prio = prio;
		}

		public void InjectDependencies(IAgent agent, IIKComponent ik, AgentArmsComponent component,
			TransformLookup lookup, RigidbodyWrapper rigidbodyWrapper, FinalIKComponent finalIKComponent)
		{
			this.agent = agent;
			this.component = component;
			this.ik = ik;
			this.lookup = lookup;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.finalIKComponent = finalIKComponent;

			hand = lookup.Lookup(IsLeft ? HumanBoneIdentifiers.LEFT_HAND : HumanBoneIdentifiers.RIGHT_HAND);
		}

		public void Dispose()
		{
			ik.RemoveInfluencer(this, IKChain);
			ElbowHintWeight = 0f;
		}

		public void Reset()
		{
			targetPosSmooth = Vector3.zero;
			posVelocity = Vector3.zero;

			ik.RemoveInfluencer(this, IKChain);
			ElbowHintWeight = 0f;
		}

		public void Update(float weight, RuntimeEquipedData data, float delta)
		{
			// GATHER CONTROL DATA.
			(Vector3 pos, Quaternion rot) orientation = component.GetHandSlotOrientation(IsLeft, false);
			Vector3 positionOffset = hand.position - orientation.pos;
			Quaternion rotationOffset = orientation.rot.Inverse() * hand.rotation;

			float mass = data.RuntimeItemData.TryGetStat(AgentStatIdentifiers.MASS, out float m) ? m : 1f;
			float strength = agent.TryGetStat(AgentStatIdentifiers.STRENGTH, out EntityStat s) ? s : 1f;
			float smoothTime = mass / (strength * 0.1f);

			// CALCULATE POSITION.
			Vector3 targetPos = hand.position - agent.Transform.position;
			targetPos -= rigidbodyWrapper.Acceleration * smoothTime;
			targetPosSmooth =
				targetPosSmooth == Vector3.zero ?
					targetPos :
					targetPosSmooth.SmoothDamp(targetPos, ref posVelocity, smoothTime, delta);

			// - Prevent position going out of bounds.
			targetPosSmooth = (targetPosSmooth + agent.Transform.position).LocalizePoint(agent.Transform);
			if (targetPosSmooth.z < 0f) targetPosSmooth.z = 0f;
			if (IsLeft && targetPosSmooth.x > 0f || !IsLeft && targetPosSmooth.x < 0f) targetPosSmooth.x = 0f;
			targetPosSmooth = targetPosSmooth.GlobalizePoint(agent.Transform) - agent.Transform.position;

			// - Prevent position passing through body.
			Vector3 flat = targetPosSmooth.FlattenY();
			if (flat.magnitude < agent.Body.Bumper.radius)
			{
				targetPosSmooth = flat.ClampMagnitude(agent.Body.Bumper.radius, float.MaxValue).SetY(targetPosSmooth.y);
			}

			// CALCULATE ROTATION.
			Quaternion targetRotation = orientation.rot;
			targetRotationSmooth =
				targetRotationSmooth == Quaternion.identity ?
					targetRotation :
					targetRotationSmooth.SmoothDamp(targetRotation, ref rotVelocity, smoothTime, delta);

			// APPLY INFLUENCE.
			ElbowHintWeight = 0.5f * weight;
			ik.AddInfluencer(this,
				IsLeft ? IKChainConstants.LEFT_ARM : IKChainConstants.RIGHT_ARM, prio,
				agent.Transform.position + targetPosSmooth + targetRotation * positionOffset, weight,
				targetRotationSmooth * rotationOffset, weight);
		}

		public void DrawGizmos()
		{
			if (hand == null)
			{
				hand = lookup.Lookup(IsLeft ? HumanBoneIdentifiers.LEFT_HAND : HumanBoneIdentifiers.RIGHT_HAND);
			}

			Gizmos.color = Color.blue;
			//Gizmos.DrawLine(hips, hand.position);
			Gizmos.color = new Color(0.5f, 1f, 0.1f, 0.6f);
			//Gizmos.DrawWireSphere(hips, 0.03f);
			Gizmos.color = Color.magenta;
			Gizmos.DrawWireSphere(hand.position, 0.03f);
			Gizmos.color = Color.red;
			Gizmos.DrawSphere(targetPosSmooth, 0.02f);
		}
	}
}
