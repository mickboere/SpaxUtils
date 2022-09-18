using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public abstract class IKComponentBase : EntityComponentBase, IIKComponent
	{
		[SerializeField] private bool fixedUpdate;

		protected Dictionary<string, Dictionary<object, IKInfluencer>> influencers = new Dictionary<string, Dictionary<object, IKInfluencer>>();

		protected void Update()
		{
			if (!fixedUpdate)
			{
				ApplyInfluencers(influencers);
			}
		}

		protected void FixedUpdate()
		{
			if (fixedUpdate)
			{
				ApplyInfluencers(influencers);
			}
		}

		public void AddInfluencer(object caller, string ikChain, Transform target, float positionWeight, float rotationWeight)
		{
			AddInfluencer(caller, ikChain, target.position, positionWeight, target.rotation, rotationWeight);
		}

		public void AddInfluencer(object caller, string ikChain, Vector3 position, float positionWeight, Quaternion rotation, float rotationWeight)
		{
			if (!influencers.ContainsKey(ikChain))
			{
				influencers.Add(ikChain, new Dictionary<object, IKInfluencer>());
			}

			influencers[ikChain][caller] = new IKInfluencer(ikChain, position, positionWeight, rotation, rotationWeight);
		}

		public void RemoveInfluencer(object caller, string ikChain)
		{
			if (influencers.ContainsKey(ikChain))
			{
				influencers[ikChain].Remove(caller);
				if (influencers[ikChain].Count == 0)
				{
					influencers.Remove(ikChain);
				}
			}
		}

		protected abstract void ApplyInfluencers(Dictionary<string, Dictionary<object, IKInfluencer>> influencers);
	}
}
