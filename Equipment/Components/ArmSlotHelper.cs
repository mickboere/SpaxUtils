using System;
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
		private AgentArmsComponent component;
		private IIKComponent ik;
		private TransformLookup lookup;
		private RigidbodyWrapper rigidbodyWrapper;
		private FinalIKComponent finalIKComponent;

		private Vector3 hips;
		private Transform hand;
		private Vector3 targetPos;
		private Vector3 posVelocity;

		private Vector3 forward;
		private Vector3 dirVelocity;

		public ArmSlotHelper(bool isLeft, int prio, AgentArmsComponent component,
			IIKComponent ik, TransformLookup lookup, RigidbodyWrapper rigidbodyWrapper, FinalIKComponent finalIKComponent)
		{
			this.prio = prio;
			this.ik = ik;
			this.component = component;
			this.lookup = lookup;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.finalIKComponent = finalIKComponent;

			IsLeft = isLeft;
		}

		public void Dispose()
		{
			ik.RemoveInfluencer(this, IKChain);
			ElbowHintWeight = 0f;
		}

		public void Reset()
		{
			targetPos = Vector3.zero;
			posVelocity = Vector3.zero;

			ik.RemoveInfluencer(this, IKChain);
			ElbowHintWeight = 0f;
		}

		public void Update(float weight, ArmedSettings settings, float delta)
		{
			// Collect data.
			(Vector3 pos, Quaternion rot) orientation = component.GetHandSlotOrientation(IsLeft, false);
			hips = lookup.Lookup(HumanBoneIdentifiers.HIPS).position;
			hand = lookup.Lookup(IsLeft ? HumanBoneIdentifiers.LEFT_HAND : HumanBoneIdentifiers.RIGHT_HAND);
			Vector3 positionOffset = hand.position - orientation.pos;
			Quaternion rotationOffset = Quaternion.Inverse(orientation.rot) * hand.rotation;

			// Calculate target position.
			Vector3 target = hips + (ik.Entity.Transform.right * (IsLeft ? -1f : 1f)) * settings.width;
			target += ik.Entity.Transform.up * settings.height;
			target += ik.Entity.Transform.forward * settings.forward;
			target += (hand.position - target) * settings.handInfluence;
			target += (rigidbodyWrapper.Velocity * settings.velocityInfluence).ClampMagnitude(settings.maxVelocityInfluence);
			target += (rigidbodyWrapper.Acceleration * settings.accelerationInfluence).ClampMagnitude(settings.maxAccelerationInfluence);
			if (targetPos == Vector3.zero)
			{
				targetPos = target;
			}
			targetPos = Vector3.SmoothDamp(targetPos, target, ref posVelocity, settings.smoothTime, float.MaxValue, delta);

			// Calculate target rotation.
			Vector3 targetForward = orientation.rot * Vector3.forward;
			targetForward += (rigidbodyWrapper.Velocity * settings.rotationVelocityInfluence).ClampMagnitude(settings.maxRotationVelocityInfluence);
			targetForward += (IsLeft ? rigidbodyWrapper.Left : rigidbodyWrapper.Right) * settings.rotationInOut;
			targetForward += rigidbodyWrapper.Up * settings.rotationUpDown;
			forward = Vector3.SmoothDamp(forward, targetForward.normalized, ref dirVelocity, settings.rotationSmoothTime, float.MaxValue, delta).normalized;
			Quaternion targetRotation = Quaternion.LookRotation(forward, orientation.rot * Vector3.up);

			// Bend goal weight
			ElbowHintWeight = settings.elbowHintWeight * weight;

			ik.AddInfluencer(this,
				IsLeft ? IKChainConstants.LEFT_ARM : IKChainConstants.RIGHT_ARM, prio,
				targetPos + targetRotation * positionOffset, weight,
				targetRotation * rotationOffset, weight);
		}

		public void DrawGizmos()
		{
			if (hand == null)
			{
				hand = lookup.Lookup(IsLeft ? HumanBoneIdentifiers.LEFT_HAND : HumanBoneIdentifiers.RIGHT_HAND);
			}

			Gizmos.color = Color.blue;
			Gizmos.DrawLine(hips, hand.position);
			Gizmos.color = new Color(0.5f, 1f, 0.1f, 0.6f);
			Gizmos.DrawWireSphere(hips, 0.03f);
			Gizmos.color = Color.magenta;
			Gizmos.DrawWireSphere(hand.position, 0.03f);
			Gizmos.color = Color.red;
			Gizmos.DrawSphere(targetPos, 0.02f);
		}
	}
}
