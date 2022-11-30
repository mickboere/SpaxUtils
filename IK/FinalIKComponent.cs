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
			if (!chainInfluencers.ContainsKey(ikChain))
			{
				return;
			}

			IKEffector effector = GetEffectorForChain(ikChain);
			if (effector == null)
			{
				return;
			}

			Chain chain = chains.FirstOrDefault((c) => c.Identifier == ikChain);
			Dictionary<object, IKInfluencer> influencers = chainInfluencers[ikChain];

			Vector3 position = Vector3Extensions.AveragePoint(chain.TipBone.position, influencers.Values.Select((i) => i.Position).ToArray(), influencers.Values.Select((i) => i.PositionWeight).ToArray());
			Quaternion rotation = QuaternionExtensions.Average(influencers.Values.Select((i) => i.Rotation).ToArray(), influencers.Values.Select((i) => i.RotationWeight).ToArray());
			chain.Target.SetPositionAndRotation(position, rotation);

			float positionWeight = influencers.Max((i) => i.Value.PositionWeight);
			effector.positionWeight = positionWeight;
			effector.rotationWeight = influencers.Max((i) => i.Value.RotationWeight);
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
