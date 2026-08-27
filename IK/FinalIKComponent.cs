using System;
using System.Collections.Generic;
using UnityEngine;
using RootMotion.FinalIK;
using System.Linq;

namespace SpaxUtils
{
	[DefaultExecutionOrder(9000)]
	public class FinalIKComponent : IKComponentBase
	{
		[Serializable]
		public class Chain
		{
			public string Identifier => identifier;
			public UpdateMode UpdateMode => updateMode;
			public Transform TipBone => tipBone;
			public Transform Target => target;

			[SerializeField, ConstDropdown(typeof(IIKChainConstants))] private string identifier;
			[SerializeField] private UpdateMode updateMode;
			[SerializeField] private Transform tipBone;
			[SerializeField] private Transform target;
		}

		public FullBodyBipedIK FullBodyIK => fullBodyIK;
		public LookAtIK LookAtIK => lookAtIk;

		protected override Dictionary<string, UpdateMode> Settings { get; set; }

		[SerializeField] protected Transform ikTransformsRoot;
		[SerializeField] protected FullBodyBipedIK fullBodyIK;
		[SerializeField] protected LookAtIK lookAtIk;
		[SerializeField] protected List<Chain> chains;

		/// <summary>A bend goal's authored state, restored when nothing is influencing it.</summary>
		private struct HintRest
		{
			public Vector3 Position;
			public float Weight;
		}

		private readonly Dictionary<string, HintRest> hintRests = new Dictionary<string, HintRest>();
		private readonly HashSet<string> hintManaged = new HashSet<string>();

		protected void Awake()
		{
			// First parent IK transforms root to entity root so that transformations to the body are ignored by IK.
			ikTransformsRoot.SetParent(Entity.Transform);
			ikTransformsRoot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

			if (fullBodyIK == null)
			{
				fullBodyIK = GetComponentInChildren<FullBodyBipedIK>();
			}

			if (lookAtIk == null)
			{
				lookAtIk = GetComponentInChildren<LookAtIK>();
			}

			Settings = new Dictionary<string, UpdateMode>();
			foreach (Chain chain in chains)
			{
				Settings[chain.Identifier] = chain.UpdateMode;

				// Remembered before anything moves it — hint influencers blend out from the authored pose.
				IKConstraintBend bend = GetBendConstraintForChain(chain.Identifier);
				if (bend != null && bend.bendGoal != null)
				{
					hintRests[chain.Identifier] = new HintRest { Position = bend.bendGoal.localPosition, Weight = bend.weight };
				}
			}
		}

		public override void ApplyInfluencer(string ikChain)
		{
			ApplyEffector(ikChain);
			ApplyHint(ikChain);
		}

		private void ApplyEffector(string ikChain)
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
			Dictionary<IKInfluencer, float> rotationWeights = WeightedUtils.GetPrioritizedNormalizedWeights(influencers.Values, i => i.Priority, i => i.RotationWeight);

			float positionSum = positionWeights.Values.Sum();
			float rotationSum = rotationWeights.Values.Sum();

			Vector3 position = chain.TipBone.position;
			position = position.Lerp(BlendPositions(positionWeights, position), positionSum);

			Quaternion rotation = chain.TipBone.rotation;
			rotation = rotation.Slerp(BlendRotations(rotationWeights, rotation), rotationSum);

			chain.Target.SetPositionAndRotation(position, rotation);

			effector.positionWeight = positionSum;
			effector.rotationWeight = rotationSum;
		}

		/// <summary>
		/// The influencers' own weighted mean, each step normalised against the weight so far. A plain
		/// running lerp leaves the fallback in the middle of the blend, bulging it off the direct path.
		/// </summary>
		private static Vector3 BlendPositions(Dictionary<IKInfluencer, float> weights, Vector3 fallback)
		{
			float running = 0f;
			foreach (KeyValuePair<IKInfluencer, float> influencer in weights)
			{
				running += influencer.Value;
				if (running > 0f)
				{
					fallback = fallback.Lerp(influencer.Key.Position, influencer.Value / running);
				}
			}
			return fallback;
		}

		/// <summary>The same mean for rotations. <see cref="BlendPositions"/>.</summary>
		private static Quaternion BlendRotations(Dictionary<IKInfluencer, float> weights, Quaternion fallback)
		{
			float running = 0f;
			foreach (KeyValuePair<IKInfluencer, float> influencer in weights)
			{
				running += influencer.Value;
				if (running > 0f)
				{
					fallback = fallback.Slerp(influencer.Key.Rotation, influencer.Value / running);
				}
			}
			return fallback;
		}

		/// <summary>
		/// Blends the chain's bend goal from its authored pose, and constrains the bend by the weight sum.
		/// An uninfluenced chain is left entirely alone — the legs author their own weight and drive their
		/// own goal transform (<see cref="AgentLegIKComponent"/>).
		/// </summary>
		private void ApplyHint(string ikChain)
		{
			if (!chainHintInfluencers.ContainsKey(ikChain))
			{
				ReleaseHint(ikChain);
				return;
			}

			IKConstraintBend bend = GetBendConstraintForChain(ikChain);
			if (bend == null || bend.bendGoal == null || !hintRests.TryGetValue(ikChain, out HintRest rest))
			{
				return;
			}

			// Blend out from the authored pose rather than from last frame's result.
			bend.bendGoal.localPosition = rest.Position;
			hintManaged.Add(ikChain);

			Dictionary<object, IKInfluencer> influencers = chainHintInfluencers[ikChain];
			Dictionary<IKInfluencer, float> weights = WeightedUtils.GetPrioritizedNormalizedWeights(influencers.Values, i => i.Priority, i => i.PositionWeight);

			float sum = weights.Values.Sum();
			Vector3 position = bend.bendGoal.position;

			bend.bendGoal.position = position.Lerp(BlendPositions(weights, position), sum);
			bend.weight = sum;
		}

		/// <summary>Hands a chain's bend goal back to whatever owned it before, exactly once.</summary>
		private void ReleaseHint(string ikChain)
		{
			if (!hintManaged.Remove(ikChain) || !hintRests.TryGetValue(ikChain, out HintRest rest))
			{
				return;
			}

			IKConstraintBend bend = GetBendConstraintForChain(ikChain);
			if (bend == null)
			{
				return;
			}

			if (bend.bendGoal != null)
			{
				bend.bendGoal.localPosition = rest.Position;
			}
			bend.weight = rest.Weight;
		}

		/// <summary>
		/// A chain's authored bend goal position in world space — the neutral a hint influencer blends from.
		/// </summary>
		public override bool TryGetHintRest(string chain, out Vector3 position)
		{
			IKConstraintBend bend = GetBendConstraintForChain(chain);
			if (bend != null && bend.bendGoal != null && hintRests.TryGetValue(chain, out HintRest rest))
			{
				position = bend.bendGoal.parent == null
					? rest.Position
					: bend.bendGoal.parent.TransformPoint(rest.Position);
				return true;
			}

			position = Vector3.zero;
			return false;
		}

		protected virtual IKConstraintBend GetBendConstraintForChain(string chain)
		{
			switch (chain)
			{
				case IKChainConstants.LEFT_ARM:
					return fullBodyIK.solver.leftArmChain.bendConstraint;
				case IKChainConstants.RIGHT_ARM:
					return fullBodyIK.solver.rightArmChain.bendConstraint;
				case IKChainConstants.LEFT_LEG:
					return fullBodyIK.solver.leftLegChain.bendConstraint;
				case IKChainConstants.RIGHT_LEG:
					return fullBodyIK.solver.rightLegChain.bendConstraint;
				default:
					// The body chain has no bend to constrain.
					return null;
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
					SpaxDebug.Error($"No IKEffector defined for {chain}.");
					return null;
			}
		}
	}
}
