using System;
using UnityEngine;

namespace SpaxUtils
{
	public class ArmSlotHelper : IDisposable
	{
		public bool IsLeft { get; }

		private IIKComponent ik;
		private TransformLookup lookup;

		public ArmSlotHelper(IIKComponent ik, TransformLookup lookup, bool isLeft)
		{
			this.ik = ik;
			this.lookup = lookup;

			IsLeft = isLeft;
		}

		public void Dispose()
		{
			ik.RemoveInfluencer(this, IKChainConstants.LEFT_ARM);
			ik.RemoveInfluencer(this, IKChainConstants.RIGHT_ARM);
		}

		public void Update(float weight, float broadness = 1f)
		{
			Transform hips = lookup.Lookup(HumanBoneIdentifiers.HIPS);
			Transform target = lookup.Lookup(IsLeft ? HumanBoneIdentifiers.LEFT_HAND : HumanBoneIdentifiers.RIGHT_HAND);

			Vector3 pos = Vector3.LerpUnclamped(hips.position, target.position, broadness);

			ik.AddInfluencer(this,
				IsLeft ? IKChainConstants.LEFT_ARM : IKChainConstants.RIGHT_ARM,
				pos, weight, Quaternion.identity, 0f);
		}
	}
}
