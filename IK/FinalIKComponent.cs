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
			public IKUpdateMode UpdateMode => updateMode;
			public Transform TipBone => tipBone;
			public Transform Target => target;

			[SerializeField, ConstDropdown(typeof(IIKChainConstants))] private string identifier;
			[SerializeField] private IKUpdateMode updateMode;
			[SerializeField] private Transform tipBone;
			[SerializeField] private Transform target;
		}

		public float LeftElbowHintWeight { get { return fullBodyIK.solver.leftArmChain.bendConstraint.weight; } set { fullBodyIK.solver.leftArmChain.bendConstraint.weight = value; } }
		public float RightElbowHintWeight { get { return fullBodyIK.solver.rightArmChain.bendConstraint.weight; } set { fullBodyIK.solver.rightArmChain.bendConstraint.weight = value; } }

		protected override Dictionary<string, IKUpdateMode> Settings { get; set; }

		[SerializeField] protected FullBodyBipedIK fullBodyIK;
		[SerializeField] protected List<Chain> chains;

		protected void Awake()
		{
			if (fullBodyIK == null)
			{
				fullBodyIK = GetComponentInChildren<FullBodyBipedIK>();
			}

			Settings = new Dictionary<string, IKUpdateMode>();
			foreach (Chain chain in chains)
			{
				Settings[chain.Identifier] = chain.UpdateMode;
			}
		}

		public override void ApplyInfluencer(string ikChain)
		{
			IKEffector effector = GetEffectorForChain(ikChain);
			if (effector == null)
			{
				return;
			}

			if (!chainInfluencers.ContainsKey(ikChain))
			{
				effector.positionWeight = 0f;
				effector.rotationWeight = 0f;
				return;
			}

			Chain chain = chains.FirstOrDefault((c) => c.Identifier == ikChain);
			Dictionary<object, IKInfluencer> influencers = chainInfluencers[ikChain];

			Dictionary<IKInfluencer, float> positionWeights = WeightedUtils.GetPrioritizedNormalizedWeights(influencers.Values, i => i.Priority, i => i.PositionWeight);
			Vector3 position = chain.TipBone.position;
			foreach (KeyValuePair<IKInfluencer, float> influencer in positionWeights)
			{
				position = position.Lerp(influencer.Key.Position, influencer.Value);
			}

			Dictionary<IKInfluencer, float> rotationWeights = WeightedUtils.GetPrioritizedNormalizedWeights(influencers.Values, i => i.Priority, i => i.RotationWeight);
			Quaternion rotation = chain.TipBone.rotation;
			foreach (KeyValuePair<IKInfluencer, float> influencer in rotationWeights)
			{
				rotation = rotation.Lerp(influencer.Key.Rotation, influencer.Value);
			}

			chain.Target.SetPositionAndRotation(position, rotation);

			effector.positionWeight = positionWeights.Values.Sum();
			effector.rotationWeight = rotationWeights.Values.Sum();
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
					SpaxDebug.Error($"No IKEffector defined for {chain}.");
					return null;
			}
		}
	}
}
