using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

namespace SpaxUtils
{
	public class AnimRigIKComponent : IKComponentBase
	{
		[Serializable]
		public class Chain
		{
			public string Identifier => identifier;
			public Transform TipBone => tipBone;
			public GameObject Constraint => constraint;
			public Transform Target => target;

			[SerializeField, ConstDropdown(typeof(IIKChainConstants))] private string identifier;
			[SerializeField] private Transform tipBone;
			[SerializeField] private GameObject constraint;
			[SerializeField] private Transform target;
		}

		[SerializeField] private List<Chain> chains;

		private Dictionary<Transform, ExtractTransformConstraint> extracts = new Dictionary<Transform, ExtractTransformConstraint>();
		private Dictionary<GameObject, IRigConstraint> constraints = new Dictionary<GameObject, IRigConstraint>();

		protected void Awake()
		{
			CollectExtracts();
		}

		protected override void ApplyInfluencers(Dictionary<string, Dictionary<object, IKInfluencer>> influencers)
		{
			//foreach (Chain chain in chains)
			//{
			//	GetConstraint(chain.Constraint).weight = 0f;
			//}

			//foreach (KeyValuePair<string, Dictionary<object, IKInfluencer>> kvp in influencers)
			//{
			//	Chain chain = chains.FirstOrDefault((c) => c.Identifier == kvp.Key);
			//	if (chain == null)
			//	{
			//		continue;
			//	}

			//	IRigConstraint constraint = GetConstraint(chain.Constraint);
			//	(Vector3 position, Quaternion rotation) extract = GetRawBoneData(chain.TipBone);
			//	Vector3 position = Vector3.zero;
			//	float positionInfluence = 0f;
			//	Vector3 forwardDirection = Vector3.zero;
			//	Vector3 upDirection = Vector3.zero;
			//	float directionInfluence = 0f;

			//	chain.Target.position = extract.position;
			//	chain.Target.rotation = extract.rotation;

			//	foreach (IKInfluencer ik in kvp.Value.Values)
			//	{
			//		if (ik.Position.HasValue)
			//		{
			//			position += (ik.Position.Value - chain.Target.position) * ik.Weight;
			//			positionInfluence += ik.Weight;
			//		}
			//		if (ik.Rotation.HasValue)
			//		{
			//			forwardDirection += (ik.Rotation.Value * Vector3.forward - chain.Target.forward) * ik.Weight;
			//			upDirection += (ik.Rotation.Value * Vector3.up - chain.Target.up) * ik.Weight;
			//			directionInfluence += ik.Weight;
			//		}
			//	}

			//	if (positionInfluence > Mathf.Epsilon)
			//	{
			//		chain.Target.position += position / positionInfluence;
			//	}
			//	if (directionInfluence > Mathf.Epsilon)
			//	{
			//		chain.Target.forward += forwardDirection / directionInfluence;
			//		chain.Target.up += upDirection / directionInfluence;
			//	}

			//	constraint.weight = kvp.Value.Max((i) => i.Value.Weight);
			//}
		}

		private IRigConstraint GetConstraint(GameObject gameObject)
		{
			if (!constraints.ContainsKey(gameObject))
			{
				IRigConstraint[] foundConstraints = gameObject.GetComponents<IRigConstraint>();

				ExtractTransformConstraint transformExtract = foundConstraints.FirstOrDefault((c) => c is ExtractTransformConstraint) as ExtractTransformConstraint;
				if (transformExtract != null && !extracts.ContainsKey(transformExtract.data.bone))
				{
					extracts.Add(transformExtract.data.bone, transformExtract);
				}

				IRigConstraint rigConstraint = foundConstraints.FirstOrDefault((c) => c != transformExtract);
				constraints.Add(gameObject, rigConstraint);
				SpaxDebug.Log($"GetConstraints: ", $"({gameObject.name}), extract={transformExtract}, constraint={rigConstraint}, all=({string.Join(' ', foundConstraints.Select((f) => f.component.name))})");
			}

			return constraints[gameObject];
		}

		private void CollectExtracts()
		{
			ExtractTransformConstraint[] extractors = gameObject.GetComponentsInChildren<ExtractTransformConstraint>();
			foreach (ExtractTransformConstraint extractor in extractors)
			{
				if (!extracts.ContainsKey(extractor.data.bone))
				{
					extracts.Add(extractor.data.bone, extractor);
				}
			}
		}

		public (Vector3 position, Quaternion rotation) GetRawBoneData(Transform bone)
		{
			if (!extracts.ContainsKey(bone))
			{
				return (bone.position, bone.rotation);
			}

			ExtractTransformConstraint extract = extracts[bone];
			return (extract.data.position, extract.data.rotation);
		}
	}
}
