using System;
using System.Collections.Generic;
using UnityEngine;
using RootMotion.FinalIK;
using System.Linq;

namespace SpaxUtils
{
	public class FinalIKComponent : IKComponentBase
	{
		[Serializable]
		public class Chain
		{
			public string Identifier => identifier;
			public Transform TipBone => tipBone;
			public Transform Target => target;

			[SerializeField, ConstDropdown(typeof(IIKChainConstants))] private string identifier;
			[SerializeField] private Transform tipBone;
			[SerializeField] private Transform target;
		}

		[SerializeField] protected FullBodyBipedIK fullBodyIK;
		[SerializeField] protected List<Chain> chains;

		protected void Awake()
		{
			if (fullBodyIK == null)
			{
				fullBodyIK = GetComponentInChildren<FullBodyBipedIK>();
			}
		}

		protected override void ApplyInfluencers(Dictionary<string, Dictionary<object, IKInfluencer>> influencers)
		{
			foreach (KeyValuePair<string, Dictionary<object, IKInfluencer>> kvp in influencers)
			{
				IKEffector effector = GetEffectorForChain(kvp.Key);
				if (effector == null)
				{
					continue;
				}
				Chain chain = chains.FirstOrDefault((c) => c.Identifier == kvp.Key);

				Vector3 position = Vector3Extensions.AveragePoint(chain.TipBone.position, kvp.Value.Values.Select((i) => i.Position).ToArray(), kvp.Value.Values.Select((i) => i.PositionWeight).ToArray());
				Quaternion rotation = QuaternionExtensions.Average(kvp.Value.Values.Select((i) => i.Rotation).ToArray(), kvp.Value.Values.Select((i) => i.RotationWeight).ToArray());
				chain.Target.SetPositionAndRotation(position, rotation);

				float positionWeight = kvp.Value.Max((i) => i.Value.PositionWeight);
				effector.positionWeight = positionWeight;
				effector.rotationWeight = kvp.Value.Max((i) => i.Value.RotationWeight);
			}
		}

		protected virtual IKEffector GetEffectorForChain(string chain)
		{
			switch (chain)
			{
				case IKChainConstants.BODY:
					return fullBodyIK.solver.bodyEffector;
				case IKChainConstants.LEFT_ARM:
					return fullBodyIK.solver.leftHandEffector;
				case IKChainConstants.RIGHT_ARM:
					return fullBodyIK.solver.rightHandEffector;
				case IKChainConstants.LEFT_LEG:
					return fullBodyIK.solver.leftFootEffector;
				case IKChainConstants.RIGHT_LEG:
					return fullBodyIK.solver.rightFootEffector;
				default:
					return null;
			}
		}
	}
}
