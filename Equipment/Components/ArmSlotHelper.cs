using System;
using UnityEngine;

namespace SpaxUtils
{
	public class ArmSlotHelper : IDisposable
	{
		public bool IsLeft { get; }

		private IIKComponent ik;
		private Func<bool, bool, (Vector3, Quaternion)> orientationFunc;
		private TransformLookup lookup;
		private RigidbodyWrapper rigidbodyWrapper;

		private Vector3 hips;
		private Transform hand;
		private Vector3 targetPos;
		private Vector3 posVelocity;

		private Vector3 forward;
		private Vector3 dirVelocity;

		public ArmSlotHelper(bool isLeft, Func<bool, bool, (Vector3, Quaternion)> orientationFunc,
			IIKComponent ik, TransformLookup lookup, RigidbodyWrapper rigidbodyWrapper)
		{
			this.ik = ik;
			this.orientationFunc = orientationFunc;
			this.lookup = lookup;
			this.rigidbodyWrapper = rigidbodyWrapper;

			IsLeft = isLeft;
		}

		public void Dispose()
		{
			if (IsLeft) { ik.RemoveInfluencer(this, IKChainConstants.LEFT_ARM); }
			else { ik.RemoveInfluencer(this, IKChainConstants.RIGHT_ARM); }
		}

		public void Reset()
		{
			targetPos = Vector3.zero;
			posVelocity = Vector3.zero;

			if (IsLeft) { ik.RemoveInfluencer(this, IKChainConstants.LEFT_ARM); }
			else { ik.RemoveInfluencer(this, IKChainConstants.RIGHT_ARM); }
		}

		public void Update(float weight, ArmedSettings settings, float delta)
		{
			// Collect data.
			(Vector3 pos, Quaternion rot) orientation = orientationFunc(IsLeft, false);
			hips = lookup.Lookup(HumanBoneIdentifiers.HIPS).position;
			hand = lookup.Lookup(IsLeft ? HumanBoneIdentifiers.LEFT_HAND : HumanBoneIdentifiers.RIGHT_HAND);
			Vector3 positionOffset = hand.position - orientation.pos;
			Quaternion rotationOffset = Quaternion.Inverse(orientation.rot) * hand.rotation;

			// Calculate target position.
			Vector3 target = hips + (ik.Entity.Transform.right * (IsLeft ? -1f : 1f)) * settings.broadness;
			target += ik.Entity.Transform.up * settings.height;
			target += ik.Entity.Transform.forward * settings.forward;
			target += (hand.position - target) * settings.handInfluence;
			target += rigidbodyWrapper.Velocity * settings.velocityInfluence;
			target += rigidbodyWrapper.Acceleration * settings.accelerationInfluence;
			if (targetPos == Vector3.zero)
			{
				targetPos = target;
			}
			targetPos = Vector3.SmoothDamp(targetPos, target, ref posVelocity, settings.smoothTime, float.MaxValue, delta);

			// Calculate target rotation.
			Vector3 targetForward = orientation.rot * Vector3.forward;
			targetForward += rigidbodyWrapper.Velocity * settings.rotationVelocityInfluence;
			forward = Vector3.SmoothDamp(forward, targetForward.normalized, ref dirVelocity, settings.rotationSmoothTime, float.MaxValue, delta).normalized;
			Quaternion targetRotation = Quaternion.LookRotation(forward, orientation.rot * Vector3.up);

			ik.AddInfluencer(this,
				IsLeft ? IKChainConstants.LEFT_ARM : IKChainConstants.RIGHT_ARM,
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
