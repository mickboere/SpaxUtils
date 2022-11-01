using System;
using UnityEngine;

namespace SpaxUtils
{
	public class ArmSlotHelper : IDisposable
	{
		public bool IsLeft { get; }

		private IIKComponent ik;
		private TransformLookup lookup;
		private RigidbodyWrapper rigidbodyWrapper;

		private Vector3 hips;
		private Vector3 hand;
		private Vector3 targetPos;
		private Vector3 velocity;

		public ArmSlotHelper(bool isLeft, IIKComponent ik, TransformLookup lookup, RigidbodyWrapper rigidbodyWrapper)
		{
			this.ik = ik;
			this.lookup = lookup;
			this.rigidbodyWrapper = rigidbodyWrapper;

			IsLeft = isLeft;
		}

		public void Dispose()
		{
			ik.RemoveInfluencer(this, IKChainConstants.LEFT_ARM);
			ik.RemoveInfluencer(this, IKChainConstants.RIGHT_ARM);
		}

		public void Update(float weight, ArmTargetingSettings settings, float delta)
		{
			// Calculate target arm position.
			hips = lookup.Lookup(HumanBoneIdentifiers.HIPS).position;
			hand = lookup.Lookup(IsLeft ? HumanBoneIdentifiers.LEFT_HAND : HumanBoneIdentifiers.RIGHT_HAND).position;
			Vector3 target = hips + (ik.Entity.Transform.right * (IsLeft ? -1f : 1f)) * settings.broadness;
			target += (hand - target) * settings.handInfluence;
			target += rigidbodyWrapper.Velocity * settings.velocityInfluence;
			target += rigidbodyWrapper.Acceleration * settings.accelerationInfluence;
			if (targetPos == Vector3.zero)
			{
				targetPos = target;
			}
			targetPos = Vector3.SmoothDamp(targetPos, target, ref velocity, settings.smoothTime, float.MaxValue, delta);

			// Calculate target hand rotation.
			//Quaternion targetRot = // Create function that can convert target hand rot / pos to real rotpos, similar to FEET.

			ik.AddInfluencer(this,
				IsLeft ? IKChainConstants.LEFT_ARM : IKChainConstants.RIGHT_ARM,
				targetPos, weight, Quaternion.identity, 0f);
		}

		public void DrawGizmos()
		{
			Gizmos.color = Color.blue;
			Gizmos.DrawLine(hips, hand);
			Gizmos.color = new Color(0.5f, 1f, 0.1f, 0.6f);
			Gizmos.DrawWireSphere(hips, 0.03f);
			Gizmos.color = Color.magenta;
			Gizmos.DrawWireSphere(hand, 0.03f);
			Gizmos.color = Color.red;
			Gizmos.DrawSphere(targetPos, 0.02f);
		}
	}
}
